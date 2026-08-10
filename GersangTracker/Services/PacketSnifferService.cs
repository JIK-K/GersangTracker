using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using SharpPcap;
using PacketDotNet;

namespace GersangTracker.Services
{
    public class DroppedItemEventArgs : EventArgs
    {
        public int Pid { get; set; } // 추가됨: 어떤 프로세스(클라이언트)에서 드랍된 것인지 식별
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime DroppedAt { get; set; }
    }
    public class StatusLogEventArgs : EventArgs
    {
        public int Pid { get; set; } = -1; // -1이면 전역 메시지
        public string Message { get; set; } = string.Empty;
    }

    public class PacketSnifferService : IDisposable
    {
        // _gersangPid 단일 변수 대신 포트와 PID 매핑을 저장하는 딕셔너리로 대체
        private Dictionary<ushort, int> _portToPid = new Dictionary<ushort, int>();
        private HashSet<ushort> _gersangPorts = new HashSet<ushort>();

        private bool _isRunning = false;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private ItemDatabaseService _dbService;

        private Dictionary<string, List<byte>> _connectionBuffers = new Dictionary<string, List<byte>>();
        private Dictionary<string, HashSet<uint>> _seenSequences = new Dictionary<string, HashSet<uint>>();

        public event EventHandler<DroppedItemEventArgs>? ItemDropped;
        //public event Action<string>? StatusLog;
        public event EventHandler<StatusLogEventArgs>? StatusLog;

        public PacketSnifferService(ItemDatabaseService dbService)
        {
            _dbService = dbService;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            _connectionBuffers.Clear();

            Task.Run(() => CaptureLoop(_cts.Token));
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _cts.Cancel();
        }

        private void CaptureLoop(CancellationToken token)
        {
            StatusLog?.Invoke(this, new StatusLogEventArgs { Message = "거상 트래커 준비 중..." });

            bool foundAnyGersang = false;
            while (!foundAnyGersang && !token.IsCancellationRequested)
            {
                var processes = Process.GetProcessesByName("gersang");
                if (processes.Length > 0)
                {
                    foundAnyGersang = true;
                    StatusLog?.Invoke(this, new StatusLogEventArgs { Message = $"거상 프로세스 감지됨 (현재 {processes.Length}개 실행 중)" });
                }
                else
                {
                    Thread.Sleep(2000);
                }
            }

            if (token.IsCancellationRequested) return;

            Task.Run(() => UpdatePortsLoop(token));

            var devices = CaptureDeviceList.Instance;
            if (devices.Count < 1)
            {
                StatusLog?.Invoke(this, new StatusLogEventArgs { Message = "오류: Npcap 디바이스를 찾을 수 없습니다." });
                return;
            }

            foreach (var device in devices)
            {
                try
                {
                    device.OnPacketArrival += Device_OnPacketArrival;
                    device.Open(DeviceModes.Promiscuous, 1000);
                    device.StartCapture();
                }
                catch { }
            }

            StatusLog?.Invoke(this, new StatusLogEventArgs { Message = "[준비 완료]" });

            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(1000);
            }

            foreach (var device in devices)
            {
                try
                {
                    device.StopCapture();
                    device.Close();
                }
                catch { }
            }
        }

        private void UpdatePortsLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 현재 실행 중인 모든 거상 프로세스 PID 수집
                    var processes = Process.GetProcessesByName("gersang");
                    if (processes.Length == 0)
                    {
                        Thread.Sleep(3000);
                        continue;
                    }
                    var activePids = processes.Select(p => p.Id).ToHashSet();

                    var netstat = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "netstat",
                            Arguments = "-ano",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    netstat.Start();
                    string output = netstat.StandardOutput.ReadToEnd();
                    netstat.WaitForExit();

                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var newPortToPid = new Dictionary<ushort, int>();

                    foreach (var line in lines)
                    {
                        var parts = Regex.Split(line.Trim(), @"\s+");
                        if (parts.Length >= 5)
                        {
                            string local = parts[1];
                            string pidStr = parts[4];

                            if (int.TryParse(pidStr, out int pid) && activePids.Contains(pid))
                            {
                                var localParts = local.Split(':');
                                if (localParts.Length >= 2 && ushort.TryParse(localParts.Last(), out ushort port))
                                {
                                    newPortToPid[port] = pid;
                                }
                            }
                        }
                    }

                    lock (_gersangPorts)
                    {
                        foreach (var kvp in newPortToPid)
                        {
                            if (!_portToPid.ContainsKey(kvp.Key) || _portToPid[kvp.Key] != kvp.Value)
                            {
                                _portToPid[kvp.Key] = kvp.Value;
                                _gersangPorts.Add(kvp.Key);
                                StatusLog?.Invoke(this, new StatusLogEventArgs { Message = $"거상 통신 포트 감지: {kvp.Key} (PID: {kvp.Value})" });
                            }
                        }
                    }
                }
                catch { }

                Thread.Sleep(3000);
            }
        }

        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            var rawPacket = e.GetPacket();
            var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

            var ipPacket = packet.Extract<IPPacket>();
            if (ipPacket == null) return;

            string srcIp = ipPacket.SourceAddress.ToString();
            string dstIp = ipPacket.DestinationAddress.ToString();

            var tcpPacket = packet.Extract<TcpPacket>();
            var udpPacket = packet.Extract<UdpPacket>();

            ushort srcPort = 0;
            ushort dstPort = 0;
            byte[] payload = null;
            string connectionKey = "";

            if (tcpPacket != null)
            {
                srcPort = tcpPacket.SourcePort;
                dstPort = tcpPacket.DestinationPort;
                payload = tcpPacket.PayloadData;
                connectionKey = $"TCP_{srcIp}:{srcPort}-{dstIp}:{dstPort}";
            }
            else if (udpPacket != null)
            {
                srcPort = udpPacket.SourcePort;
                dstPort = udpPacket.DestinationPort;
                payload = udpPacket.PayloadData;
                connectionKey = $"UDP_{srcIp}:{srcPort}-{dstIp}:{dstPort}";
            }

            if (payload == null || payload.Length == 0) return;

            if (tcpPacket != null)
            {
                uint seq = tcpPacket.SequenceNumber;
                lock (_seenSequences)
                {
                    if (!_seenSequences.ContainsKey(connectionKey))
                        _seenSequences[connectionKey] = new HashSet<uint>();

                    if (_seenSequences[connectionKey].Contains(seq))
                        return;

                    _seenSequences[connectionKey].Add(seq);

                    if (_seenSequences[connectionKey].Count > 1000)
                        _seenSequences[connectionKey].Clear();
                }
            }

            int targetPid = -1;
            bool isGersangPacket = false;

            lock (_gersangPorts)
            {
                if (_portToPid.TryGetValue(srcPort, out int pid))
                {
                    isGersangPacket = true;
                    targetPid = pid;
                }
                else if (_portToPid.TryGetValue(dstPort, out pid))
                {
                    isGersangPacket = true;
                    targetPid = pid;
                }
            }

            if (isGersangPacket)
            {
                lock (_connectionBuffers)
                {
                    ParseGersangPacket(connectionKey, payload, targetPid);
                }
            }
        }

        private void ParseGersangPacket(string connectionKey, byte[] payload, int targetPid)
        {
            if (!_connectionBuffers.ContainsKey(connectionKey))
                _connectionBuffers[connectionKey] = new List<byte>();

            var buffer = _connectionBuffers[connectionKey];
            buffer.AddRange(payload);

            // 너무 크면 비움 (메모리 누수 방지)
            if (buffer.Count > 1024 * 1024)
            {
                buffer.Clear();
                return;
            }

            while (buffer.Count >= 2)
            {
                int packetLength = BitConverter.ToUInt16(buffer.ToArray(), 0);

                if (packetLength < 4 || packetLength > 6000)
                {
                    buffer.RemoveAt(0); // 1바이트 쉬프트하며 복구 시도
                    continue;
                }

                if (buffer.Count >= packetLength)
                {
                    byte unk = buffer[2]; // 보통 00
                    if (unk != 0x00)
                    {
                        buffer.RemoveAt(0);
                        continue;
                    }

                    byte[] fullPacket = buffer.Take(packetLength).ToArray();
                    buffer.RemoveRange(0, packetLength);

                    ProcessFullPacket(fullPacket, targetPid);
                }
                else
                {
                    break;
                }
            }
        }

        private void ProcessFullPacket(byte[] payload, int targetPid)
        {
            if (payload.Length < 5) return;

            string hex = BitConverter.ToString(payload).Replace("-", " ");
            string header = $"{payload[3]:X2} {payload[4]:X2}";

            for (int i = 0; i <= payload.Length - 62; i++)
            {
                if (payload[i] == 0x77 && payload[i + 1] == 0x27 && payload[i + 2] == 0x00 && payload[i + 3] == 0x00)
                {
                    uint id = BitConverter.ToUInt32(payload, i + 54);
                    uint qty = BitConverter.ToUInt32(payload, i + 58);

                    if (id > 0 && id < 60000 && qty > 0 && qty < 10000)
                    {
                        string itemName = _dbService.GetItemName(id.ToString());

                        if (string.IsNullOrEmpty(itemName) && id > 9472)
                        {
                            itemName = _dbService.GetItemName((id - 9472).ToString());
                        }

                        if (!string.IsNullOrEmpty(itemName))
                        {
                            ItemDropped?.Invoke(this, new DroppedItemEventArgs
                            {
                                Pid = targetPid,
                                ItemName = itemName,
                                Quantity = (int)qty,
                                DroppedAt = DateTime.Now
                            });

                            StatusLog?.Invoke(this, new StatusLogEventArgs
                            {
                                Pid = targetPid,
                                Message = $"[아이템 획득] {itemName} {qty}개를 획득했습니다!"
                            });
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
    }
}