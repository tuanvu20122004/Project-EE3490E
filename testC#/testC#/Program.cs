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

            // Khởi động gửi dữ liệu trong hai thread riêng biệt
            Thread senderThread1 = new Thread(() => sender.StartSending5ePort20015(cts.Token)); // 500Hz for port 20015
            Thread senderThread2 = new Thread(() => sender.StartSending5ePort20012(cts.Token)); // 200Hz for port 20012

            senderThread1.Start();
            senderThread2.Start();

            // Chờ người dùng nhấn phím để dừng chương trình
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();

            // Dừng việc gửi khi người dùng nhấn phím
            cts.Cancel();
            sender.Stop();
            sender.Dispose();

            // Đợi thread gửi dữ liệu kết thúc
            senderThread1.Join();
            senderThread2.Join();

            Console.WriteLine("Program stopped.");
        }
    }

    // Cập nhật lớp Sender nếu cần (giữ nguyên như đã chỉnh sửa trước đó)
    public class Sender
    {
        private Socket socket1;  // Port 20015 (500Hz)
        private Socket socket2;  // Port 20012 (200Hz)

        private readonly int frequencyHz1;   // 500
        private readonly int frequencyHz2;   // 200

        // Kích thước gói riêng cho từng cổng
        private readonly int length20015;    // 184 bytes
        private readonly int length20012;    // 70 bytes

        private readonly byte[] data20015;   // buffer cho 20015
        private readonly byte[] data20012;   // buffer cho 20012

        private long ticksPerInterval1;
        private long ticksPerInterval2;
        private volatile bool isRunning;
        private readonly BLOCK block;

        public Sender(BLOCK block = BLOCK.BLOCK_5E, int frequency1 = 500, int frequency2 = 200,
                      int lengthFor20015 = 184, int lengthFor20012 = 70)
        {
            frequencyHz1 = frequency1;
            frequencyHz2 = frequency2;

            // Tính tick/chu kỳ (độ chính xác cao)
            QueryPerformanceFrequency(out long freq);
            ticksPerInterval1 = freq / frequencyHz1;
            ticksPerInterval2 = freq / frequencyHz2;

            this.block = block;

            length20015 = lengthFor20015;
            length20012 = lengthFor20012;

            data20015 = new byte[length20015];
            data20012 = new byte[length20012];

            // Fill dữ liệu mẫu (có thể thay bằng nội dung thật)
            for (int i = 0; i < data20015.Length; i++)
                data20015[i] = (byte)(i & 0xFF);

            for (int i = 0; i < data20012.Length; i++)
                data20012[i] = (byte)(i & 0xFF);

            // Khởi tạo UDP socket (sender)
            socket1 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket1.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 64);
            socket1.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, 1024 * 1024);
            var multicastOption1 = new MulticastOption(IPAddress.Parse("224.1.1.2"), IPAddress.Any);
            socket1.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, multicastOption1);

            socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket2.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 64);
            socket2.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, 1024 * 1024);
            var multicastOption2 = new MulticastOption(IPAddress.Parse("224.1.1.2"), IPAddress.Any);
            socket2.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, multicastOption2);
        }

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFreq);

        public void StartSending5ePort20015(CancellationToken cancellationToken = default)
        {
            isRunning = true;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            Console.WriteLine($"Starting send data at {frequencyHz1}Hz (Port 20015)");
            Console.WriteLine($"Sending {data20015.Length} bytes per packet to {block} on port 20015");

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
                    // nhúng packet counter vào 4 byte đầu (nếu cần)
                    data20015[0] = (byte)((packetCount1 >> 24) & 0xFF);
                    data20015[1] = (byte)((packetCount1 >> 16) & 0xFF);
                    data20015[2] = (byte)((packetCount1 >> 8) & 0xFF);
                    data20015[3] = (byte)((packetCount1 >> 0) & 0xFF);

                    socket1.SendTo(data20015, endPoint1);
                    packetCount1++;

                    HighPrecisionTimer.SpinWaitUntil(nextSendTime1);
                    nextSendTime1 += ticksPerInterval1;

                    if (statsTimer.ElapsedMilliseconds >= 1000)
                    {
                        uint packetsThisSecond1 = packetCount1 - lastStatsPacketCount1;
                        double actualFreq1 = packetsThisSecond1 / (statsTimer.ElapsedMilliseconds / 1000.0);
                        double totalElapsed = HighPrecisionTimer.GetElapsedMilliseconds(startTicks) / 1000.0;
                        double avgFreq1 = packetCount1 / totalElapsed;

                        Console.WriteLine($"Sent {block}: {packetCount1} total to 20015, Current: {actualFreq1:F1}Hz, Average: {avgFreq1:F2}Hz");

                        lastStatsPacketCount1 = packetCount1;
                        statsTimer.Restart();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception sending to 20015: {ex.Message}");
                }
            }

            double totalTime1 = HighPrecisionTimer.GetElapsedMilliseconds(startTicks) / 1000.0;
            double avgAllFreq1 = packetCount1 / totalTime1;
            Console.WriteLine($"Sender stopped for port 20015. Total: {packetCount1} packets in {totalTime1:F2}s, Avg freq: {avgAllFreq1:F2}Hz");
        }

        public void StartSending5ePort20012(CancellationToken cancellationToken = default)
        {
            isRunning = true;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            Console.WriteLine($"Starting send data at {frequencyHz2}Hz (Port 20012)");
            Console.WriteLine($"Sending {data20012.Length} bytes per packet to {block} on port 20012");

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
                    // nhúng packet counter vào 4 byte đầu (nếu cần)
                    data20012[0] = (byte)((packetCount2 >> 24) & 0xFF);
                    data20012[1] = (byte)((packetCount2 >> 16) & 0xFF);
                    data20012[2] = (byte)((packetCount2 >> 8) & 0xFF);
                    data20012[3] = (byte)((packetCount2 >> 0) & 0xFF);

                    socket2.SendTo(data20012, endPoint2);
                    packetCount2++;

                    HighPrecisionTimer.SpinWaitUntil(nextSendTime2);
                    nextSendTime2 += ticksPerInterval2;

                    if (statsTimer.ElapsedMilliseconds >= 1000)
                    {
                        uint packetsThisSecond2 = packetCount2 - lastStatsPacketCount2;
                        double actualFreq2 = packetsThisSecond2 / (statsTimer.ElapsedMilliseconds / 1000.0);
                        double totalElapsed = HighPrecisionTimer.GetElapsedMilliseconds(startTicks) / 1000.0;
                        double avgFreq2 = packetCount2 / totalElapsed;

                        Console.WriteLine($"Sent {block}: {packetCount2} total to 20012, Current: {actualFreq2:F1}Hz, Average: {avgFreq2:F2}Hz");

                        lastStatsPacketCount2 = packetCount2;
                        statsTimer.Restart();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception sending to 20012: {ex.Message}");
                }
            }

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
