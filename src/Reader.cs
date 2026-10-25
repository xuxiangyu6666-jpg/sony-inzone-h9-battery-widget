using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading;

namespace InzoneH9Widget
{
    public sealed class Reading
    {
        public string status = "starting", message = "正在读取接收器…", port;
        public int? battery;
        public bool? charging;
        public string updatedAt, frame;
        public static Reading Fail(string status, string message, string port)
        { return new Reading { status = status, message = message, port = port }; }
    }

    // Native .NET serial API. H9 command bytes and payload offsets verified against
    // janit88/InZoneH9Tray @ 4780303dbee4bf1a86acf37c2cbc96b1774eee00.
    // HCI event length/checksum cross-checked against openinzone's protocol docs.
    public static class Reader
    {
        static readonly byte[] Init1 = Hex("01 00 FC 08 96 C3 21 01 01 01 00 7D");
        static readonly byte[] Init2 = Hex("01 00 FC 08 96 C3 41 02 01 01 00 9E");
        static readonly byte[] Query = Hex("01 00 FC 08 96 C3 41 04 01 01 00 A0");
        static byte[] Hex(string s) { return s.Split(' ').Select(x => Convert.ToByte(x, 16)).ToArray(); }

        public static string[] Ports()
        {
            var result = new List<string>();
            using (var search = new ManagementObjectSearcher("SELECT Name,PNPDeviceID FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0 AND PNPDeviceID LIKE 'USB%VID_054C&PID_0E53%'") )
            using (var devices = search.Get())
                foreach (ManagementObject device in devices)
                using (device)
                {
                    string name = Convert.ToString(device["Name"]);
                    var match = System.Text.RegularExpressions.Regex.Match(name, @"\((COM\d+)\)");
                    if (match.Success) result.Add(match.Groups[1].Value);
                }
            return result.Distinct().OrderBy(x => x).ToArray();
        }

        public static Reading Read()
        {
            string port = null;
            try
            {
                var ports = Ports();
                if (ports.Length == 0) return Reading.Fail("not_found", "未找到 H9/H7 接收器，请插入原装 USB 接收器。蓝牙连接不支持此读取方式。", null);
                if (ports.Length > 1) return Reading.Fail("multiple_devices", "发现多个 H9/H7 接收器，请只保留一个，避免显示错误设备的电量。", null);
                port = ports[0];
                using (var serial = new SerialPort(port, 115200, Parity.None, 8, StopBits.One))
                {
                    serial.ReadTimeout = 150; serial.WriteTimeout = 1500;
                    serial.Handshake = Handshake.None; serial.DtrEnable = true; serial.RtsEnable = true;
                    serial.Open(); Thread.Sleep(500);
                    serial.DiscardInBuffer(); serial.DiscardOutBuffer();
                    Exchange(serial, Init1, 900); Exchange(serial, Init2, 900);
                    // Discard pre-query notifications; only accept a fresh battery reply.
                    serial.DiscardInBuffer();
                    byte[] bytes = Exchange(serial, Query, 1800);
                    return Parse(bytes, port);
                }
            }
            catch (UnauthorizedAccessException)
            { return Reading.Fail("busy", "端口被占用或拒绝访问，请完全退出 INZONE Hub／其他电量软件，再点刷新。", port); }
            catch (TimeoutException)
            { return Reading.Fail("timeout", "接收器通信超时，将自动重试；请检查 USB 连接。", port); }
            catch (IOException)
            { return Reading.Fail("serial_error", "接收器连接中断或串口不可用，请重新插入 USB 接收器。", port); }
            catch (ManagementException)
            { return Reading.Fail("discovery_error", "Windows 设备查询失败，请检查 WMI 服务后重试。", port); }
            catch (Exception e)
            { return Reading.Fail("error", "读取失败（" + e.GetType().Name + "），将自动重试。", port); }
        }

        static byte[] Exchange(SerialPort serial, byte[] command, int milliseconds)
        {
            serial.Write(command, 0, command.Length);
            var data = new List<byte>(); var timer = Stopwatch.StartNew();
            byte[] buffer = new byte[1024];
            while (timer.ElapsedMilliseconds < milliseconds)
            {
                try
                {
                    int count = serial.Read(buffer, 0, Math.Min(buffer.Length, Math.Max(1, serial.BytesToRead)));
                    for (int i = 0; i < count; i++) data.Add(buffer[i]);
                    if (data.Count > 65536) throw new IOException("Unexpected stream length");
                }
                catch (TimeoutException) { }
            }
            return data.ToArray();
        }

        public static Reading Parse(byte[] data, string port)
        {
            Reading latest = null;
            for (int i = 0; i + 14 <= data.Length; i++)
            {
                // Exact 2-byte headset payload: reject other event IDs, sources,
                // truncated frames, bad checksum, and 0xFF/invalid percentages.
                if (data[i] != 4 || data[i+1] != 255 || data[i+2] != 11 || data[i+3] != 0 ||
                    data[i+4] != 0x96 || data[i+5] != 0xC3 || data[i+6] != 0x14 || data[i+7] != 4 ||
                    (data[i+8] != 0x10 && data[i+8] != 0x20 && data[i+8] != 0xA0)) continue;
                int sum = 0; for (int j = 3; j < 13; j++) sum += data[i+j];
                if ((byte)sum != data[i+13]) continue;
                latest = data[i+12] <= 100
                    ? new Reading { status = "ok", message = "接收器实时读取", port = port, battery = data[i+12],
                        charging = data[i+11] <= 1 ? (bool?)(data[i+11] == 1) : null,
                        updatedAt = DateTimeOffset.Now.ToString("o"), frame = BitConverter.ToString(data, i, 14) }
                    : Reading.Fail("not_reporting", "耳机暂未报告电量，请检查电源、距离及接收器 PC 模式。", port);
            }
            return latest ?? Reading.Fail("no_response", "未收到有效电量响应，请开启耳机、检查接收器 PC 模式；固件也可能不兼容。", port);
        }
    }
}
