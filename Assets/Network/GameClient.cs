using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Unity.Cinemachine;
using UnityEngine;

public class GameClient : MonoBehaviour
{
    public static GameClient Instance { get; private set; }

    public string ServerIp = "127.0.0.1";
    public int Port = 25000;
    public byte Character = 0;
    public string PlayerName = PlayerNames.DefaultName;
    public bool ConnectOnStart = true;

    public GameObject[] CharacterPrefabs;
    public Transform SpawnPoint;
    public CinemachineCamera VirtualCamera;

    private TcpClient _tcp;
    private NetworkStream _stream;
    private readonly PacketFramer _framer = new PacketFramer();

    private UdpClient _udp;
    private IPEndPoint _serverUdp;
    private IPEndPoint _recvFrom = new IPEndPoint(IPAddress.Any, 0);

    private int _myId;
    private bool _connected;
    private ushort _seq;

    private NetworkPlayer _localPlayer;
    private readonly Dictionary<int, NetworkPlayer> _remotePlayers = new Dictionary<int, NetworkPlayer>();
    private readonly Dictionary<int, Bonus> _bonuses = new Dictionary<int, Bonus>();

    public int MyId => _myId;
    public bool IsConnected => _connected;
    public bool HasLocalPlayer => _localPlayer != null;
    public int RemotePlayerCount => _remotePlayers.Count;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (ConnectOnStart) Connect();
    }

    public bool Connect()
    {
        try
        {
            if (_connected)
            {
                return true;
            }

            _tcp = new TcpClient();
            _tcp.Connect(ServerIp, Port);
            _stream = _tcp.GetStream();

            _udp = new UdpClient();
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            _serverUdp = new IPEndPoint(IPAddress.Parse(ServerIp), Port);

            PacketWriter w = new PacketWriter(MessageType.Connect);
            w.WriteByte(Character);
            w.WriteString(PlayerNames.Normalize(PlayerName));
            SendTcp(w.ToBytes());

            _connected = true;
            Debug.Log("Connected to " + ServerIp + ":" + Port);

            bool isHost = GameServer.Instance != null && GameServer.Instance.IsRunning;
            if (!isHost)
                foreach (NetworkedObject o in NetworkedObject.All) o.SetAsRemote();

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Connection failed: " + ex.Message);
            CloseNetwork();
            return false;
        }
    }

    void Update()
    {
        if (!_connected) return;
        ReceiveTcp();
        ReceiveUdp();
    }

    void OnDisable()
    {
        LeaveServer();
    }

    public void LeaveServer()
    {
        CloseNetwork();
        ClearPlayers();
        _framer.Clear();
        _seq = 0;
    }

    public void SendMove(Vector3 position, float yaw)
    {
        if (!_connected || _myId == 0) return;
        PacketWriter w = new PacketWriter(MessageType.Move, _seq++);
        w.WriteInt(_myId);
        w.WriteVector3(position);
        w.WriteFloat(yaw);
        byte[] bytes = w.ToBytes();
        _udp.Send(bytes, bytes.Length, _serverUdp);
    }

    public void SendPickupRequest(int bonusId)
    {
        if (!_connected) return;
        PacketWriter w = new PacketWriter(MessageType.PickupRequest);
        w.WriteInt(bonusId);
        SendTcp(w.ToBytes());
    }

    public void RegisterBonus(Bonus bonus)
    {
        _bonuses[bonus.BonusId] = bonus;
    }

    public bool HasRemotePlayer(int id) => _remotePlayers.ContainsKey(id);

    public NetworkPlayer GetRemotePlayer(int id)
    {
        _remotePlayers.TryGetValue(id, out NetworkPlayer player);
        return player;
    }

    private void ReceiveTcp()
    {
        try
        {
            if (IsTcpClosed(_tcp))
            {
                LeaveServer();
                return;
            }

            int available = _tcp.Available;
            if (available > 0)
            {
                byte[] tmp = new byte[available];
                int read = _stream.Read(tmp, 0, available);
                if (read == 0)
                {
                    LeaveServer();
                    return;
                }
                _framer.Push(tmp, read);
            }

            while (_framer.TryRead(out byte[] packet))
                HandleTcp(packet);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Client TCP closed: " + ex.Message);
            LeaveServer();
        }
    }

    private void HandleTcp(byte[] packet)
    {
        PacketReader r = new PacketReader(packet);
        switch (r.Type)
        {
            case MessageType.Welcome:
                _myId = r.ReadInt();
                SpawnLocalPlayer();
                break;

            case MessageType.Spawn:
                PlayerState s = r.ReadPlayerState();
                if (s.Id != _myId) SpawnRemotePlayer(s);
                break;

            case MessageType.Despawn:
                RemovePlayer(r.ReadInt());
                break;

            case MessageType.BonusState:
                int count = r.ReadInt();
                for (int i = 0; i < count; i++) DestroyBonus(r.ReadInt());
                break;

            case MessageType.PickupAck:
                int bonusId = r.ReadInt();
                int who = r.ReadInt();
                int score = r.ReadInt();
                DestroyBonus(bonusId);
                UpdateScore(who, score);
                break;
        }
    }

    private void ReceiveUdp()
    {
        while (_udp.Available > 0)
        {
            byte[] data = _udp.Receive(ref _recvFrom);
            PacketReader r = new PacketReader(data);

            if (r.Type == MessageType.Snapshot)
            {
                int count = r.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    int id = r.ReadInt();
                    Vector3 pos = r.ReadPositionQuantized();
                    float yaw = r.ReadYawQuantized();

                    if (id == _myId) continue;
                    if (_remotePlayers.TryGetValue(id, out NetworkPlayer np))
                        np.SetNetworkTarget(pos, yaw);
                }
            }
            else if (r.Type == MessageType.ObjectState)
            {
                int count = r.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    int id = r.ReadInt();
                    Vector3 pos = r.ReadPositionQuantized();
                    float yaw = r.ReadYawQuantized();

                    NetworkedObject o = NetworkedObject.Find(id);
                    if (o != null) o.SetNetworkTarget(pos, yaw);
                }
            }
        }
    }

    private void SpawnLocalPlayer()
    {
        GameObject prefab = PrefabFor(Character);
        if (prefab == null) return;

        Vector3 pos = SpawnPoint != null ? SpawnPoint.position : Vector3.zero;
        GameObject go = Instantiate(prefab, pos, Quaternion.identity);

        _localPlayer = go.GetComponent<NetworkPlayer>();
        if (_localPlayer == null) _localPlayer = go.AddComponent<NetworkPlayer>();
        _localPlayer.InitLocal(_myId, this);
        ApplyPlayerName(_localPlayer, PlayerName);

        PointCameraAt(go.transform);
    }

    private void PointCameraAt(Transform target)
    {
        if (VirtualCamera == null)
            VirtualCamera = FindFirstObjectByType<CinemachineCamera>();

        if (VirtualCamera != null)
        {
            VirtualCamera.Follow = target;
            VirtualCamera.LookAt = target;
        }
    }

    private void SpawnRemotePlayer(PlayerState s)
    {
        GameObject prefab = PrefabFor(s.Character);
        if (prefab == null) return;

        GameObject go = Instantiate(prefab, s.Position, Quaternion.Euler(0f, s.Yaw, 0f));
        go.name = "Remote Player " + s.Id;

        NetworkPlayer np = go.GetComponent<NetworkPlayer>();
        if (np == null) np = go.AddComponent<NetworkPlayer>();
        np.InitRemote(s.Id, s.Position, s.Yaw);
        ApplyPlayerName(np, s.PlayerName);

        _remotePlayers[s.Id] = np;
    }

    private static void ApplyPlayerName(NetworkPlayer player, string playerName)
    {
        if (player == null)
        {
            return;
        }

        CharacterScore score = player.GetComponentInChildren<CharacterScore>();
        if (score == null)
        {
            score = player.gameObject.AddComponent<CharacterScore>();
        }

        score.SetPlayerName(playerName);
    }

    private void RemovePlayer(int id)
    {
        if (_remotePlayers.TryGetValue(id, out NetworkPlayer np))
        {
            DestroyGameObject(np.gameObject);
            _remotePlayers.Remove(id);
        }
    }

    private void UpdateScore(int playerId, int score)
    {
        NetworkPlayer np = null;
        if (playerId == _myId) np = _localPlayer;
        else _remotePlayers.TryGetValue(playerId, out np);

        if (np == null) return;

        CharacterScore cs = np.GetComponentInChildren<CharacterScore>();
        if (cs != null) cs.SetScore(score);
    }

    private void DestroyBonus(int bonusId)
    {
        if (_bonuses.TryGetValue(bonusId, out Bonus b))
        {
            if (b != null) Destroy(b.gameObject);
            _bonuses.Remove(bonusId);
        }
    }

    private GameObject PrefabFor(byte character)
    {
        if (CharacterPrefabs != null && character < CharacterPrefabs.Length && CharacterPrefabs[character] != null)
            return CharacterPrefabs[character];

        Debug.LogError("No prefab assigned for character " + character);
        return null;
    }

    private void SendTcp(byte[] bytes)
    {
        try { _stream.Write(bytes, 0, bytes.Length); }
        catch (System.Exception e) { Debug.LogWarning(e.Message); }
    }

    private void CloseNetwork()
    {
        if (_tcp != null)
        {
            try { _tcp.Client.Shutdown(SocketShutdown.Both); } catch { }
            try { _tcp.Close(); } catch { }
            _tcp = null;
        }

        _stream = null;

        if (_udp != null)
        {
            try { _udp.Close(); } catch { }
            _udp = null;
        }

        _serverUdp = null;
        _connected = false;
    }

    private void ClearPlayers()
    {
        if (_localPlayer != null)
        {
            DestroyGameObject(_localPlayer.gameObject);
            _localPlayer = null;
        }

        foreach (NetworkPlayer player in _remotePlayers.Values)
        {
            if (player != null)
            {
                DestroyGameObject(player.gameObject);
            }
        }

        _remotePlayers.Clear();
        _myId = 0;
    }

    private static bool IsTcpClosed(TcpClient client)
    {
        try
        {
            Socket socket = client.Client;
            return socket == null ||
                !socket.Connected ||
                (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0);
        }
        catch (SocketException)
        {
            return true;
        }
        catch (System.ObjectDisposedException)
        {
            return true;
        }
    }

    private static void DestroyGameObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
