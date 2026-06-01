using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class TestClient : MonoBehaviour
{
    public string ServerIp = "127.0.0.1";
    public int Port = 25000;
    public byte Character = 0;
    public float MovesPerSecond = 10f;

    private TcpClient _tcp;
    private NetworkStream _stream;
    private readonly PacketFramer _framer = new PacketFramer();

    private UdpClient _udp;
    private IPEndPoint _serverUdp;
    private IPEndPoint _recvFrom = new IPEndPoint(IPAddress.Any, 0);

    private int _myId;
    private float _moveTimer;
    private ushort _seq;

    void Start()
    {
        try
        {
            _tcp = new TcpClient();
            _tcp.Connect(ServerIp, Port);
            _stream = _tcp.GetStream();

            _udp = new UdpClient();
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            _serverUdp = new IPEndPoint(IPAddress.Parse(ServerIp), Port);

            PacketWriter w = new PacketWriter(MessageType.Connect);
            w.WriteByte(Character);
            SendTcp(w.ToBytes());

            Debug.Log("TestClient connected to " + ServerIp + ":" + Port);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("TestClient connection failed: " + ex.Message);
        }
    }

    void Update()
    {
        ReceiveTcp();
        ReceiveUdp();

        if (_myId == 0) return;

        _moveTimer += Time.deltaTime;
        if (_moveTimer >= 1f / MovesPerSecond)
        {
            _moveTimer = 0f;
            SendMove();
        }
    }

    void OnDisable()
    {
        if (_tcp != null) { _tcp.Close(); _tcp = null; }
        if (_udp != null) { _udp.Close(); _udp = null; }
    }

    private void SendMove()
    {
        Vector3 pos = new Vector3(Mathf.Sin(Time.time), 0f, Mathf.Cos(Time.time));
        PacketWriter w = new PacketWriter(MessageType.Move, _seq++);
        w.WriteInt(_myId);
        w.WriteVector3(pos);
        w.WriteFloat(0f);
        byte[] bytes = w.ToBytes();
        _udp.Send(bytes, bytes.Length, _serverUdp);
    }

    private void ReceiveTcp()
    {
        if (_tcp == null) return;

        int available = _tcp.Available;
        if (available > 0)
        {
            byte[] tmp = new byte[available];
            int read = _stream.Read(tmp, 0, available);
            _framer.Push(tmp, read);
        }

        while (_framer.TryRead(out byte[] packet))
            HandleTcp(packet);
    }

    private void HandleTcp(byte[] packet)
    {
        PacketReader r = new PacketReader(packet);
        switch (r.Type)
        {
            case MessageType.Welcome:
                _myId = r.ReadInt();
                Debug.Log("Welcome! My id = " + _myId);
                break;

            case MessageType.Spawn:
                PlayerState s = r.ReadPlayerState();
                Debug.Log("Spawn player " + s.Id + " (character " + s.Character + ")");
                break;

            case MessageType.Despawn:
                Debug.Log("Despawn player " + r.ReadInt());
                break;

            case MessageType.BonusState:
                int taken = r.ReadInt();
                string ids = "";
                for (int i = 0; i < taken; i++) ids += r.ReadInt() + " ";
                Debug.Log("BonusState: " + taken + " bonus already taken [ " + ids + "]");
                break;

            case MessageType.PickupAck:
                int bonus = r.ReadInt();
                int who = r.ReadInt();
                int score = r.ReadInt();
                Debug.Log("Bonus " + bonus + " taken by player " + who + " (score " + score + ")");
                break;
        }
    }

    private void ReceiveUdp()
    {
        if (_udp == null) return;

        while (_udp.Available > 0)
        {
            byte[] data = _udp.Receive(ref _recvFrom);
            PacketReader r = new PacketReader(data);

            if (r.Type == MessageType.Snapshot)
            {
                int count = r.ReadInt();
                string line = "Snapshot (" + count + " players): ";
                for (int i = 0; i < count; i++)
                {
                    int id = r.ReadInt();
                    Vector3 pos = r.ReadPositionQuantized();
                    float yaw = r.ReadYawQuantized();
                    line += "[" + id + " @ " + pos.ToString("F1") + " yaw " + yaw.ToString("F0") + "] ";
                }
                Debug.Log(line);
            }
        }
    }

    private void SendTcp(byte[] bytes)
    {
        _stream.Write(bytes, 0, bytes.Length);
    }
}
