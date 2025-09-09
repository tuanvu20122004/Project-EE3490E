using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace UDPForward
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Sender sender = new Sender(block: BLOCK.BLOCK_5E, frequency1: 500, frequency2: 200);
            CancellationTokenSource cts = new CancellationTokenSource();

            Thread senderThread1 = new Thread(() => sender.StartSending5ePort20015(cts.Token)); // 500Hz -> 5E
            Thread senderThread2 = new Thread(() => sender.StartSending5ePort20012(cts.Token)); // 200Hz -> Tele

            senderThread1.Start();
            senderThread2.Start();

            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();

            cts.Cancel();
            sender.Stop();
            sender.Dispose();

            senderThread1.Join();
            senderThread2.Join();

            Console.WriteLine("Program stopped.");
            Console.WriteLine($"Total packets sent: {sender.PacketCount1 + sender.PacketCount2}");
        }
    }

    public class Sender
    {
        private const int LENGTH_DATA_20015 = 184; // 5E
        private const int LENGTH_DATA_20012 = 70;  // Tele

        public uint PacketCount1 { get; private set; }
        public uint PacketCount2 { get; private set; }

        private Socket socket1;  // 5E -> 20015
        private Socket socket2;  // Tele -> 20012
        private int frequencyHz1;
        private int frequencyHz2;
        private byte[] dataToSend1; // 5E buffer
        private byte[] dataToSend2; // Tele buffer
        private long ticksPerInterval1;
        private long ticksPerInterval2;
        private bool isRunning;
        private BLOCK block;

        public Sender(BLOCK block = BLOCK.BLOCK_5E, int frequency1 = 500, int frequency2 = 200)
        {
            frequencyHz1 = frequency1;
            frequencyHz2 = frequency2;

            QueryPerformanceFrequency(out long freq);
            ticksPerInterval1 = freq / frequencyHz1;
            ticksPerInterval2 = freq / frequencyHz2;

            this.block = block;

            // --- Init buffers with proper headers/tailers ---
            dataToSend1 = new byte[LENGTH_DATA_20015]; // 5E
            dataToSend1[0] = 0xAB;
            dataToSend1[1] = 0xCD;
            dataToSend1[2] = 0xEF;
            // Tailer E1 E2 E3 placed at LENGTH-5 .. LENGTH-3
            dataToSend1[LENGTH_DATA_20015 - 5] = 0xE1;
            dataToSend1[LENGTH_DATA_20015 - 4] = 0xE2;
            dataToSend1[LENGTH_DATA_20015 - 3] = 0xE3;
            // Last 2 bytes are CRC16-1021 (set during send)

            dataToSend2 = new byte[LENGTH_DATA_20012]; // Tele
            dataToSend2[0] = 0xAF;                     // Header
            dataToSend2[LENGTH_DATA_20012 - 3] = 0xFA; // Tailer at byte 67
            // Last 2 bytes are CRC16-8005 (set during send)

            // --- Sockets ---
            socket1 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket1.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 64);
            socket1.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, 1024 * 1024);
            socket1.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(IPAddress.Parse("224.1.1.2"), IPAddress.Any));

            socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket2.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 64);
            socket2.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, 1024 * 1024);
            socket2.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(IPAddress.Parse("224.1.1.2"), IPAddress.Any));
        }

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFreq);

        // ------------------- Port 20015: 5E (184B) -------------------
        public void StartSending5ePort20015(CancellationToken cancellationToken = default)
        {
            isRunning = true;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            Console.WriteLine($"Starting send data at {frequencyHz1}Hz (Port 20015, 5E)");
            Console.WriteLine($"Sending {dataToSend1.Length} bytes per packet");

            uint packetCount1 = 0;
            long startTicks = HighPrecisionTimer.GetTicks();

            var statsTimer = Stopwatch.StartNew();
            uint lastStatsPacketCount1 = 0;

            long nextSendTime1 = HighPrecisionTimer.GetTicks() + ticksPerInterval1;
            EndPoint endPoint1 = new IPEndPoint(IPAddress.Parse("224.1.1.2"), 20015);

            while (isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 18-bit counter spread across bytes [3],[4],[5]
                    dataToSend1[3] = (byte)((packetCount1 >> 10) & 0xFF);
                    dataToSend1[4] = (byte)((packetCount1 >> 2) & 0xFF);
                    dataToSend1[5] = (byte)(((packetCount1 & 0x03) << 6) & 0xC0);
      
                    // CRC16-1021 over all bytes except the last 2 CRC bytes
                    ushort crc = CalculateCrc16_1021(dataToSend1, LENGTH_DATA_20015 - 2);
                    dataToSend1[LENGTH_DATA_20015 - 2] = (byte)((crc >> 8) & 0xFF);
                    dataToSend1[LENGTH_DATA_20015 - 1] = (byte)(crc & 0xFF);

                    socket1.SendTo(dataToSend1, endPoint1);
                    packetCount1++;

                    HighPrecisionTimer.SpinWaitUntil(nextSendTime1);
                    nextSendTime1 += ticksPerInterval1;

                    if (statsTimer.ElapsedMilliseconds >= 1000)
                    {
                        uint packetsThisSecond1 = packetCount1 - lastStatsPacketCount1;
                        double actualFreq1 = packetsThisSecond1 / (statsTimer.ElapsedMilliseconds / 1000.0);
                        double totalElapsed = HighPrecisionTimer.GetElapsedMilliseconds(startTicks) / 1000.0;
                        double avgFreq1 = packetCount1 / totalElapsed;

                        Console.WriteLine($"5E -> 20015: {packetCount1} total, Current: {actualFreq1:F1}Hz, Average: {avgFreq1:F2}Hz");

                        lastStatsPacketCount1 = packetCount1;
                        statsTimer.Restart();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception sending to 20015: {ex.Message}");
                }
            }
            PacketCount1 = packetCount1;
            double totalTime1 = HighPrecisionTimer.GetElapsedMilliseconds(startTicks) / 1000.0;
            double avgAllFreq1 = packetCount1 / totalTime1;
            Console.WriteLine($"Sender stopped for port 20015. Total: {packetCount1} packets in {totalTime1:F2}s, Avg freq: {avgAllFreq1:F2}Hz");
        }

        // ------------------- Port 20012: Tele (70B) -------------------
        public void StartSending5ePort20012(CancellationToken cancellationToken = default)
        {
            isRunning = true;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            Console.WriteLine($"Starting send data at {frequencyHz2}Hz (Port 20012, Tele)");
            Console.WriteLine($"Sending {dataToSend2.Length} bytes per packet");

            uint packetCount2 = 0;
            long startTicks = HighPrecisionTimer.GetTicks();

            var statsTimer = Stopwatch.StartNew();
            uint lastStatsPacketCount2 = 0;

            long nextSendTime2 = HighPrecisionTimer.GetTicks() + ticksPerInterval2;
            EndPoint endPoint2 = new IPEndPoint(IPAddress.Parse("224.1.1.2"), 20012);

            while (isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 24-bit counter big-endian into bytes [1..3]
                    dataToSend2[1] = (byte)((packetCount2 >> 16) & 0xFF);
                    dataToSend2[2] = (byte)((packetCount2 >> 8) & 0xFF);
                    dataToSend2[3] = (byte)(packetCount2 & 0xFF);
                    
                    // Make sure tailer is present at byte 67
                    dataToSend2[LENGTH_DATA_20012 - 3] = 0xFA;

                    // CRC16-8005 over bytes [0..67], write to [68..69] big-endian
                    ushort crc = CalculateCrc16_8005(dataToSend2, LENGTH_DATA_20012 - 2);
                    dataToSend2[LENGTH_DATA_20012 - 2] = (byte)((crc >> 8) & 0xFF);
                    dataToSend2[LENGTH_DATA_20012 - 1] = (byte)(crc & 0xFF);

                    socket2.SendTo(dataToSend2, endPoint2);
                    packetCount2++;

                    HighPrecisionTimer.SpinWaitUntil(nextSendTime2);
                    nextSendTime2 += ticksPerInterval2;

                    if (statsTimer.ElapsedMilliseconds >= 1000)
                    {
                        uint packetsThisSecond2 = packetCount2 - lastStatsPacketCount2;
                        double actualFreq2 = packetsThisSecond2 / (statsTimer.ElapsedMilliseconds / 1000.0);
                        double totalElapsed = HighPrecisionTimer.GetElapsedMilliseconds(startTicks) / 1000.0;
                        double avgFreq2 = packetCount2 / totalElapsed;

                        Console.WriteLine($"Tele -> 20012: {packetCount2} total, Current: {actualFreq2:F1}Hz, Average: {avgFreq2:F2}Hz");

                        lastStatsPacketCount2 = packetCount2;
                        statsTimer.Restart();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception sending to 20012: {ex.Message}");
                }
            }
            PacketCount2 = packetCount2;
            double totalTime2 = HighPrecisionTimer.GetElapsedMilliseconds(startTicks) / 1000.0;
            double avgAllFreq2 = packetCount2 / totalTime2;
            Console.WriteLine($"Sender stopped for port 20012. Total: {packetCount2} packets in {totalTime2:F2}s, Avg freq: {avgAllFreq2:F2}Hz");
        }
        public void Stop() => isRunning = false;

        public void Dispose()
        {
            socket1?.Close();
            socket1?.Dispose();
            socket2?.Close();
            socket2?.Dispose();
        }

        // ------------------- CRC helpers -------------------
        // CRC16-1021 (poly 0x1021), init 0xFFFF, no reflect, no xorout
        private static ushort CalculateCrc16_1021(byte[] data, int lengthWithoutCrc)
        {
            const ushort poly = 0x1021;
            ushort crc = 0xFFFF;

            for (int i = 0; i < lengthWithoutCrc; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int b = 0; b < 8; b++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ poly);
                    else
                        crc <<= 1;
                }
            }
            return crc;
        }

        // CRC16-8005 (poly 0x8005), init 0x0000, no reflect, no xorout
        private static ushort CalculateCrc16_8005(byte[] data, int lengthWithoutCrc)
        {
            const ushort poly = 0x8005;
            ushort crc = 0x0000;

            for (int i = 0; i < lengthWithoutCrc; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int b = 0; b < 8; b++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ poly);
                    else
                        crc <<= 1;
                }
            }
            return crc;
        }
    }

    public enum BLOCK
    {
        BLOCK_5U = 1,
        BLOCK_5A = 2,
        BLOCK_5E = 3,
        BLOCK_TELE = 4,
    }

    public static class HighPrecisionTimer
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryPerformanceFrequency(out long lpFreq);

        private static readonly long Frequency;
        private static readonly double FrequencyInverse;

        static HighPrecisionTimer()
        {
            QueryPerformanceFrequency(out Frequency);
            FrequencyInverse = 1.0 / Frequency;
        }

        public static long GetTicks()
        {
            QueryPerformanceCounter(out long ticks);
            return ticks;
        }

        public static double GetElapsedMilliseconds(long startTicks)
        {
            QueryPerformanceCounter(out long currentTicks);
            return (currentTicks - startTicks) * FrequencyInverse * 1000.0;
        }

        public static void SpinWaitUntil(long targetTicks)
        {
            while (GetTicks() < targetTicks)
            {
                Thread.SpinWait(1);
            }
        }
    }
}
