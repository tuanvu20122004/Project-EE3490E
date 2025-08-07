#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import socket
import time
import ctypes
import sys

# Đảm bảo chỉ chạy trên Windows
if sys.platform != 'win32':
    raise OSError("Chương trình này chỉ hỗ trợ trên Windows")

# Gọi WinMM để thay đổi timer resolution
_winmm = ctypes.WinDLL('winmm')
# Đặt timer resolution xuống 1ms
_winmm.timeBeginPeriod(1)

MULTICAST_GROUP = "224.1.1.2"
PORTS = [20012, 20015]
MESSAGE = b"TestMulticastData"
INTERVAL = 0.002       # 2ms giữa mỗi lần gửi
BUSY_WAIT = 0.005      # 5ms cuối sẽ busy-wait

def send_multicast_precise():
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)
    sock.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_TTL, 2)

    start = time.perf_counter()
    count = 1
    index = 1  # Khởi tạo biến index

    try:
        with open("log.txt", "a") as log_file:
            while True:
                target = start + count * INTERVAL

                # Đợi bằng sleep cho đến khi chỉ còn BUSY_WAIT
                now = time.perf_counter()
                to_sleep = target - now - BUSY_WAIT
                if to_sleep > 0:
                    time.sleep(to_sleep)

                # Cuối cùng busy-wait cho đến phút giây chính xác
                while time.perf_counter() < target:
                    pass
                ts = time.perf_counter()
                for port in PORTS:
                    message_with_index = f"{MESSAGE.decode()}-{index}".encode()  # Thêm index vào message
                    sock.sendto(message_with_index, (MULTICAST_GROUP, port))
                    entry = f"Sent to {MULTICAST_GROUP}:{port} -> {message_with_index.decode()} at {ts:.3f}s\n"
                    print(entry.strip())
                    log_file.write(entry)
                log_file.flush()

                # Tăng index mỗi lần gửi
                index += 1
                count += 1

    except KeyboardInterrupt:
        print("Stopped by user.")
    finally:
        sock.close()
        # Trả lại timer resolution mặc định
        _winmm.timeEndPeriod(1)

if __name__ == "__main__":
    send_multicast_precise()
