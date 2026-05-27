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
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime DroppedAt { get; set; }
    }

    public class PacketSnifferService : IDisposable
    {
        private HashSet<ushort> _gersangPorts = new HashSet<ushort>();
        private int _gersangPid = -1;
        private bool _isRunning = false;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private ItemDatabaseService _dbService;

        private Dictionary<string, List<byte>> _connectionBuffers = new Dictionary<string, List<byte>>();

        private Dictionary<string, HashSet<uint>> _seenSequences = new Dictionary<string, HashSet<uint>>();

        public event EventHandler<DroppedItemEventArgs>? ItemDropped;
        public event Action<string>? StatusLog;

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
            StatusLog?.Invoke("거상 패킷 스니핑 준비 중...");

            while (_gersangPid == -1 && !token.IsCancellationRequested)
            {
                var processes = Process.GetProcessesByName("gersang");
                if (processes.Length > 0)
                {
                    _gersangPid = processes[0].Id;
                    StatusLog?.Invoke($"거상 프로세스 감지됨 (PID: {_gersangPid})");
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
                StatusLog?.Invoke("오류: Npcap 디바이스를 찾을 수 없습니다.");
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

            StatusLog?.Invoke("TCP 패킷 조립(Reassembly) 기능이 적용된 스니핑 시작!");
            StatusLog?.Invoke("TCP 패킷 조립(Reassembly) 기능이 적용된 스니핑 시작");

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
                    var newPorts = new HashSet<ushort>();

                    foreach (var line in lines)
                    {
                        if (line.Contains(_gersangPid.ToString()))
                        {
                            var parts = Regex.Split(line.Trim(), @"\s+");
                            if (parts.Length >= 4)
                            {
                                string local = parts[1];
                                var localParts = local.Split(':');
                                if (localParts.Length >= 2 && ushort.TryParse(localParts.Last(), out ushort port))
                                {
                                    newPorts.Add(port);
                                }
                            }
                        }
                    }

                    lock (_gersangPorts)
                    {
                        foreach (var port in newPorts)
                        {
                            if (!_gersangPorts.Contains(port))
                            {
                                _gersangPorts.Add(port);
                                StatusLog?.Invoke($"거상 통신 포트 감지: {port}");
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

            bool isGersangPacket = false;
            lock (_gersangPorts)
            {
                if (_gersangPorts.Contains(srcPort) || _gersangPorts.Contains(dstPort))
                {
                    isGersangPacket = true;
                }
            }

            if (isGersangPacket)
            {
                lock (_connectionBuffers)
                {
                    ParseGersangPacket(connectionKey, payload);
                }
            }
        }

        private void ParseGersangPacket(string connectionKey, byte[] payload)
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

                // 패킷 길이가 4 미만이거나 6000 이상이면 Sync 박살 (Gersang 패킷은 보통 4000 이하)
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

                    ProcessFullPacket(fullPacket);
                }
                else
                {
                    break;
                }
            }
        }

        private void ProcessFullPacket(byte[] payload)
        {
            if (payload.Length < 5) return;

            // 모든 패킷을 로그에 기록 (사용자가 나중에 확인할 수 있도록)
            string hex = BitConverter.ToString(payload).Replace("-", " ");
            string header = $"{payload[3]:X2} {payload[4]:X2}";
            File.AppendAllText("FullPacketLog.txt", $"[{DateTime.Now:HH:mm:ss.fff}] [{header}] Length:{payload.Length} | {hex}\n");

            // F0 03 패킷은 전투 종료 후 모든 용병의 전체 인벤토리 상태를 동기화하는 패킷입니다.
            // 인벤토리 상태를 이전과 비교하여 새롭게 증가한 수량만 드랍으로 판별합니다.
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
                                ItemName = itemName,
                                Quantity = (int)qty,
                                DroppedAt = DateTime.Now
                            });

                            StatusLog?.Invoke($"[아이템 획득] {itemName} {qty}개를 획득했습니다!");
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