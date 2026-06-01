using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class GameServer : MonoBehaviour
{
    public int Port = 25000;
    public float SnapshotsPerSecond = 20f;

    private class Client
    {
        public int Id;
        public TcpClient Tcp;
        public NetworkStream Stream;
        public IPEndPoint UdpEndpoint;
        public PlayerState State;
        public int Score;
        public readonly PacketFramer Framer = new PacketFramer();
    }

    private TcpListener _tcpListener;
    private UdpClient _udp;
    private IPEndPoint _udpSource = new IPEndPoint(IPAddress.Any, 0);

    private readonly Dictionary<int, Client> _clients = new Dictionary<int, Client>();
    private readonly HashSet<int> _takenBonuses = new HashSet<int>();

    private int _nextId = 1;
    private float _snapshotTimer;

    void Start()
    {
        _tcpListener = new TcpListener(IPAddress.Any, Port);
        _tcpListener.Start();

        _udp = new UdpClient();
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.ExclusiveAddressUse = false;
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, Port));

        Debug.Log("Server started on port " + Port);
    }

    void Update()
    {
        AcceptConnections();
        ReceiveTcp();
        ReceiveUdp();

        _snapshotTimer += Time.deltaTime;
        if (_snapshotTimer >= 1f / SnapshotsPerSecond)
        {
            _snapshotTimer = 0f;
            SendSnapshot();
        }
    }

    void OnDisable()
    {
        if (_tcpListener != null) { _tcpListener.Stop(); _tcpListener = null; }
        if (_udp != null) { _udp.Close(); _udp = null; }
        _clients.Clear();
    }

    private void AcceptConnections()
    {
        while (_tcpListener.Pending())
        {
            TcpClient tcp = _tcpListener.AcceptTcpClient();
            int id = _nextId++;

            Client c = new Client
            {
                Id = id,
                Tcp = tcp,
                Stream = tcp.GetStream(),
                State = new PlayerState { Id = id, Character = 0, Position = SpawnPosition(id), Yaw = 0f },
            };
            _clients.Add(id, c);
            Debug.Log("Client connected, assigned id " + id);
        }
    }

    private void ReceiveTcp()
    {
        List<int> dead = null;

        foreach (Client c in _clients.Values)
        {
            try
            {
                int available = c.Tcp.Available;
                if (available > 0)
                {
                    byte[] tmp = new byte[available];
                    int read = c.Stream.Read(tmp, 0, available);
                    if (read == 0)
                    {
                        (dead ??= new List<int>()).Add(c.Id);
                        continue;
                    }
                    c.Framer.Push(tmp, read);
                    while (c.Framer.TryRead(out byte[] packet))
                        HandleTcpPacket(c, packet);
                }
                else if (!c.Tcp.Connected)
                {
                    (dead ??= new List<int>()).Add(c.Id);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("TCP error on client " + c.Id + ": " + ex.Message);
                (dead ??= new List<int>()).Add(c.Id);
            }
        }

        if (dead != null)
            foreach (int id in dead) RemoveClient(id);
    }

    private void HandleTcpPacket(Client c, byte[] packet)
    {
        PacketReader r = new PacketReader(packet);
        switch (r.Type)
        {
            case MessageType.Connect:
                c.State.Character = r.ReadByte();
                SendWelcome(c);
                SendExistingPlayersTo(c);
                BroadcastSpawn(c.State);
                break;

            case MessageType.PickupRequest:
                HandlePickup(c, r.ReadInt());
                break;
        }
    }

    private void ReceiveUdp()
    {
        while (_udp.Available > 0)
        {
            byte[] data = _udp.Receive(ref _udpSource);
            PacketReader r = new PacketReader(data);

            if (r.Type == MessageType.Move)
            {
                int id = r.ReadInt();
                Vector3 pos = r.ReadVector3();
                float yaw = r.ReadFloat();

                if (_clients.TryGetValue(id, out Client c))
                {
                    c.UdpEndpoint = new IPEndPoint(_udpSource.Address, _udpSource.Port);
                    c.State.Position = pos;
                    c.State.Yaw = yaw;
                }
            }
        }
    }

    private void HandlePickup(Client c, int bonusId)
    {
        if (_takenBonuses.Contains(bonusId)) return;

        _takenBonuses.Add(bonusId);
        c.Score += 1;

        PacketWriter w = new PacketWriter(MessageType.PickupAck);
        w.WriteInt(bonusId);
        w.WriteInt(c.Id);
        w.WriteInt(c.Score);
        BroadcastTcp(w.ToBytes());
    }

    private void SendWelcome(Client c)
    {
        PacketWriter w = new PacketWriter(MessageType.Welcome);
        w.WriteInt(c.Id);
        SendTcp(c, w.ToBytes());
    }

    private void SendExistingPlayersTo(Client newClient)
    {
        foreach (Client c in _clients.Values)
        {
            if (c.Id == newClient.Id) continue;
            PacketWriter w = new PacketWriter(MessageType.Spawn);
            w.WritePlayerState(c.State);
            SendTcp(newClient, w.ToBytes());
        }
    }

    private void BroadcastSpawn(PlayerState state)
    {
        PacketWriter w = new PacketWriter(MessageType.Spawn);
        w.WritePlayerState(state);
        byte[] bytes = w.ToBytes();
        foreach (Client c in _clients.Values)
            if (c.Id != state.Id) SendTcp(c, bytes);
    }

    private void SendSnapshot()
    {
        if (_clients.Count == 0) return;

        PacketWriter w = new PacketWriter(MessageType.Snapshot);
        w.WriteInt(_clients.Count);
        foreach (Client c in _clients.Values)
            w.WritePlayerState(c.State);
        byte[] bytes = w.ToBytes();

        foreach (Client c in _clients.Values)
        {
            if (c.UdpEndpoint == null) continue;
            try { _udp.Send(bytes, bytes.Length, c.UdpEndpoint); }
            catch (SocketException e) { Debug.LogWarning(e.Message); }
        }
    }

    private void RemoveClient(int id)
    {
        if (!_clients.TryGetValue(id, out Client c)) return;

        try { c.Tcp.Close(); } catch { }
        _clients.Remove(id);
        Debug.Log("Client " + id + " disconnected");

        PacketWriter w = new PacketWriter(MessageType.Despawn);
        w.WriteInt(id);
        BroadcastTcp(w.ToBytes());
    }

    private void SendTcp(Client c, byte[] bytes)
    {
        if (c == null || !c.Tcp.Connected) return;
        try { c.Stream.Write(bytes, 0, bytes.Length); }
        catch (System.Exception e) { Debug.LogWarning(e.Message); }
    }

    private void BroadcastTcp(byte[] bytes)
    {
        foreach (Client c in _clients.Values) SendTcp(c, bytes);
    }

    private Vector3 SpawnPosition(int id)
    {
        return new Vector3((id % 4) * 2f, 0f, (id / 4) * 2f);
    }
}
