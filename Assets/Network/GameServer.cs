using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class GameServer : MonoBehaviour
{
    public int Port = 25000;
    public float SnapshotsPerSecond = 20f;
    public int FullSnapshotEvery = 30;
    public float MatchDurationSeconds = 180f;
    public bool AutoStart = false;
    public float BonusRespawnDelaySeconds = 5f;
    public Transform[] BonusSpawnPoints;
    public int TargetActiveBonusCount = 500;
    public int MinimumActiveBonusCount = 500;
    public bool RandomizeInitialBonusPositions = true;
    public Vector3 BonusSpawnAreaCenter = new Vector3(240.5f, 0f, 254.2f);
    public Vector2 BonusSpawnAreaSize = new Vector2(345f, 312f);
    public float BonusSpawnHeightOffset = 0.28f;
    public float BonusSpawnRaycastHeight = 50f;
    public LayerMask BonusSpawnGroundLayers = -1;

    private class Client
    {
        public int Id;
        public TcpClient Tcp;
        public NetworkStream Stream;
        public IPEndPoint UdpEndpoint;
        public PlayerState State;
        public PlayerState LastSent;
        public bool EverSent;
        public int Score;
        public readonly PacketFramer Framer = new PacketFramer();
    }

    private class BonusRuntimeState
    {
        public int Id;
        public Vector3 Position;
        public int Points;
        public bool Active = true;
        public float RespawnTimeRemaining;
    }

    private TcpListener _tcpListener;
    private UdpClient _udp;
    private IPEndPoint _udpSource = new IPEndPoint(IPAddress.Any, 0);

    private readonly Dictionary<int, Client> _clients = new Dictionary<int, Client>();
    private readonly Dictionary<int, BonusRuntimeState> _bonusStates = new Dictionary<int, BonusRuntimeState>();
    private readonly List<Vector3> _bonusSpawnPositions = new List<Vector3>();

    private int _nextId = 1;
    private int _nextBonusId;
    private float _snapshotTimer;
    private int _snapshotCounter;
    private bool _bonusStatesInitialized;
    
    private float _matchTimeRemaining;
    private bool _matchStarted;
    private bool _matchEnded;
    private float _timerSyncTimer;

    public bool IsRunning => _tcpListener != null;

    public static GameServer Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        if (AutoStart) StartServer();
    }

    public bool StartServer()
    {
        if (IsRunning) return true;

        try
        {
            _tcpListener = new TcpListener(IPAddress.Any, Port);
            _tcpListener.Start();

            _udp = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.ExclusiveAddressUse = false;
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, Port));

            Debug.Log("Server started on port " + Port);
            InitializeBonusStatesFromScene();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Could not start server: " + ex.Message);
            if (_tcpListener != null) { _tcpListener.Stop(); _tcpListener = null; }
            if (_udp != null) { _udp.Close(); _udp = null; }
            return false;
        }
    }

    void Update()
    {
        if (!IsRunning) return;
        AcceptConnections();
        ReceiveTcp();
        ReceiveUdp();
        UpdateBonusRespawns();

        if (_matchEnded) return;

        UpdateMatchTimer();

        _snapshotTimer += Time.deltaTime;
        if (_snapshotTimer >= 1f / SnapshotsPerSecond)
        {
            _snapshotTimer = 0f;
            SendSnapshot();
            SendObjectStates();
        }
    }

    void OnDisable()
    {
        StopServer();
    }

    public void StopServer()
    {
        if (_tcpListener != null) { _tcpListener.Stop(); _tcpListener = null; }
        if (_udp != null) { _udp.Close(); _udp = null; }
        foreach (Client c in _clients.Values)
        {
            try { c.Tcp.Close(); } catch { }
        }
        _clients.Clear();
        _bonusStates.Clear();
        _bonusSpawnPositions.Clear();
        _bonusStatesInitialized = false;

        _matchStarted = false;
        _matchEnded = false;
        _matchTimeRemaining = 0f;
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
                State = new PlayerState
                {
                    Id = id,
                    Character = 0,
                    PlayerName = PlayerNames.DefaultName,
                    Score = 0,
                    Position = SpawnPosition(id),
                    Yaw = 0f,
                },
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
                if (IsClientDisconnected(c))
                {
                    (dead ??= new List<int>()).Add(c.Id);
                    continue;
                }

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
                else if (IsClientDisconnected(c))
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

    private bool IsClientDisconnected(Client c)
    {
        try
        {
            Socket socket = c.Tcp.Client;
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

    private void HandleTcpPacket(Client c, byte[] packet)
    {
        PacketReader r = new PacketReader(packet);
        switch (r.Type)
        {
            case MessageType.Connect:
                c.State.Character = r.ReadByte();
                c.State.PlayerName = PlayerNames.Normalize(r.ReadString());
                SendWelcome(c);
                SendExistingPlayersTo(c);
                SendBonusState(c);
                BroadcastSpawn(c.State);
                
                StartMatchIfNeeded();
                SendTimerSync(c);
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
        if (_matchEnded) return;

        BonusRuntimeState bonus = EnsureBonusState(bonusId);
        if (!bonus.Active) return;

        bonus.Active = false;
        bonus.RespawnTimeRemaining = Mathf.Max(0f, BonusRespawnDelaySeconds);
        c.Score += Mathf.Max(1, bonus.Points);
        c.State.Score = c.Score;

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

    private void SendBonusState(Client c)
    {
        InitializeBonusStatesFromScene();

        PacketWriter w = new PacketWriter(MessageType.BonusState);
        w.WriteInt(_bonusStates.Count);
        foreach (BonusRuntimeState bonus in _bonusStates.Values)
        {
            w.WriteInt(bonus.Id);
            w.WriteByte(bonus.Active ? (byte)1 : (byte)0);
            w.WriteVector3(bonus.Position);
        }
        SendTcp(c, w.ToBytes());
    }

    private void InitializeBonusStatesFromScene()
    {
        if (_bonusStatesInitialized) return;

        _bonusStatesInitialized = true;
        _bonusStates.Clear();
        _bonusSpawnPositions.Clear();

        ConfigureBonusSpawnAreaFromTerrain();

        if (BonusSpawnPoints != null)
        {
            foreach (Transform spawnPoint in BonusSpawnPoints)
            {
                if (spawnPoint != null)
                    AddBonusSpawnPosition(spawnPoint.position);
            }
        }

        Bonus[] sceneBonuses = FindObjectsByType<Bonus>(FindObjectsInactive.Include);
        foreach (Bonus sceneBonus in sceneBonuses)
        {
            Vector3 scenePosition = sceneBonus.transform.position;
            AddBonusSpawnPosition(scenePosition);

            if (_bonusStates.ContainsKey(sceneBonus.BonusId)) continue;

            Vector3 position = RandomizeInitialBonusPositions ? SelectBonusSpawnPosition() : scenePosition;
            _bonusStates.Add(sceneBonus.BonusId, CreateBonusState(sceneBonus.BonusId, position, sceneBonus.Points));
            _nextBonusId = Mathf.Max(_nextBonusId, sceneBonus.BonusId + 1);
        }

        EnsureTargetBonusCount();
        Debug.Log("Bonus pool initialized: " + _bonusStates.Count + " bonuses (target " + TargetActiveBonusCount + ", minimum " + MinimumActiveBonusCount + ") across area center " + BonusSpawnAreaCenter + " size " + BonusSpawnAreaSize);
    }

    private BonusRuntimeState EnsureBonusState(int bonusId)
    {
        InitializeBonusStatesFromScene();

        if (_bonusStates.TryGetValue(bonusId, out BonusRuntimeState existing))
            return existing;

        Vector3 position = SelectBonusSpawnPosition();
        AddBonusSpawnPosition(position);

        var created = CreateBonusState(bonusId, position, 1);
        _bonusStates.Add(bonusId, created);
        _nextBonusId = Mathf.Max(_nextBonusId, bonusId + 1);
        return created;
    }

    private void EnsureTargetBonusCount()
    {
        int configuredTargetCount = Mathf.Max(0, TargetActiveBonusCount);
        int minimumTargetCount = Mathf.Max(0, MinimumActiveBonusCount);
        int targetCount = Mathf.Max(configuredTargetCount, minimumTargetCount);
        while (_bonusStates.Count < targetCount)
        {
            int bonusId = _nextBonusId++;
            _bonusStates.Add(bonusId, CreateBonusState(bonusId, SelectBonusSpawnPosition(), 1));
        }
    }

    private static BonusRuntimeState CreateBonusState(int id, Vector3 position, int points)
    {
        return new BonusRuntimeState
        {
            Id = id,
            Position = position,
            Points = Mathf.Max(1, points),
            Active = true,
        };
    }

    private void UpdateBonusRespawns()
    {
        if (_bonusStates.Count == 0) return;

        List<BonusRuntimeState> respawned = null;

        foreach (BonusRuntimeState bonus in _bonusStates.Values)
        {
            if (bonus.Active) continue;

            if (BonusRespawnDelaySeconds > 0f)
                bonus.RespawnTimeRemaining -= Time.deltaTime;

            if (BonusRespawnDelaySeconds > 0f && bonus.RespawnTimeRemaining > 0f)
                continue;

            bonus.Active = true;
            bonus.Position = SelectBonusSpawnPosition();
            (respawned ??= new List<BonusRuntimeState>()).Add(bonus);
        }

        if (respawned == null) return;

        foreach (BonusRuntimeState bonus in respawned)
            BroadcastBonusSpawn(bonus);
    }

    private void BroadcastBonusSpawn(BonusRuntimeState bonus)
    {
        PacketWriter w = new PacketWriter(MessageType.BonusSpawn);
        w.WriteInt(bonus.Id);
        w.WriteVector3(bonus.Position);
        BroadcastTcp(w.ToBytes());
    }

    private Vector3 SelectBonusSpawnPosition()
    {
        if (BonusSpawnAreaSize.x > 0f && BonusSpawnAreaSize.y > 0f)
            return SelectRandomBonusSpawnPosition();

        if (_bonusSpawnPositions.Count == 0)
            return Vector3.zero;

        return _bonusSpawnPositions[Random.Range(0, _bonusSpawnPositions.Count)];
    }

    private Vector3 SelectRandomBonusSpawnPosition()
    {
        float halfWidth = BonusSpawnAreaSize.x * 0.5f;
        float halfDepth = BonusSpawnAreaSize.y * 0.5f;
        float x = Random.Range(BonusSpawnAreaCenter.x - halfWidth, BonusSpawnAreaCenter.x + halfWidth);
        float z = Random.Range(BonusSpawnAreaCenter.z - halfDepth, BonusSpawnAreaCenter.z + halfDepth);
        Vector3 fallback = new Vector3(x, BonusSpawnAreaCenter.y + BonusSpawnHeightOffset, z);

        if (BonusSpawnGroundLayers.value == 0 || BonusSpawnRaycastHeight <= 0f)
            return fallback;

        Vector3 origin = new Vector3(x, BonusSpawnAreaCenter.y + BonusSpawnRaycastHeight, z);
        float distance = BonusSpawnRaycastHeight * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, BonusSpawnGroundLayers, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * BonusSpawnHeightOffset;

        return fallback;
    }

    private void AddBonusSpawnPosition(Vector3 position)
    {
        foreach (Vector3 existing in _bonusSpawnPositions)
        {
            if ((existing - position).sqrMagnitude < 0.0001f)
                return;
        }

        _bonusSpawnPositions.Add(position);
    }

    private void ConfigureBonusSpawnAreaFromTerrain()
    {
        if (BonusSpawnAreaSize.x > 0f && BonusSpawnAreaSize.y > 0f)
            return;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
            terrain = FindAnyObjectByType<Terrain>();

        if (terrain == null || terrain.terrainData == null)
            return;

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        BonusSpawnAreaCenter = terrainPosition + new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f);
        BonusSpawnAreaSize = new Vector2(terrainSize.x, terrainSize.z);
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

        _snapshotCounter++;
        bool keyframe = (_snapshotCounter % FullSnapshotEvery) == 0;

        List<Client> toSend = new List<Client>();
        foreach (Client c in _clients.Values)
        {
            if (keyframe || HasMoved(c))
            {
                toSend.Add(c);
                c.LastSent = c.State;
                c.EverSent = true;
            }
        }

        if (toSend.Count == 0) return;

        PacketWriter w = new PacketWriter(MessageType.Snapshot);
        w.WriteInt(toSend.Count);
        foreach (Client c in toSend)
        {
            w.WriteInt(c.State.Id);
            w.WritePositionQuantized(c.State.Position);
            w.WriteYawQuantized(c.State.Yaw);
        }
        byte[] bytes = w.ToBytes();

        foreach (Client c in _clients.Values)
        {
            if (c.UdpEndpoint == null) continue;
            try { _udp.Send(bytes, bytes.Length, c.UdpEndpoint); }
            catch (SocketException e) { Debug.LogWarning(e.Message); }
        }
    }

    private void SendObjectStates()
    {
        var objects = NetworkedObject.All;
        if (objects.Count == 0) return;

        NetworkedObject.EnsureIds();

        PacketWriter w = new PacketWriter(MessageType.ObjectState);
        w.WriteInt(objects.Count);
        foreach (NetworkedObject o in objects)
        {
            w.WriteInt(o.ObjectId);
            w.WritePositionQuantized(o.transform.position);
            w.WriteYawQuantized(o.transform.eulerAngles.y);
        }
        byte[] bytes = w.ToBytes();

        foreach (Client c in _clients.Values)
        {
            if (c.UdpEndpoint == null) continue;
            try { _udp.Send(bytes, bytes.Length, c.UdpEndpoint); }
            catch (SocketException e) { Debug.LogWarning(e.Message); }
        }
    }

    private bool HasMoved(Client c)
    {
        if (!c.EverSent) return true;
        bool posChanged = (c.State.Position - c.LastSent.Position).sqrMagnitude > 0.0001f;
        bool yawChanged = Mathf.Abs(Mathf.DeltaAngle(c.State.Yaw, c.LastSent.Yaw)) > 1f;
        return posChanged || yawChanged;
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

    private void StartMatchIfNeeded()
    {
        if (_matchStarted) return;
        _matchStarted = true;
        _matchTimeRemaining = Mathf.Max(1f, MatchDurationSeconds);
        _timerSyncTimer = 0f;
    }

    private void UpdateMatchTimer()
    {
        if (!_matchStarted || _matchEnded) return;

        _matchTimeRemaining -= Time.deltaTime;

        _timerSyncTimer += Time.deltaTime;
        if (_timerSyncTimer >= 1f)
        {
            _timerSyncTimer = 0f;
            BroadcastTimerSync();
        }

        if (_matchTimeRemaining <= 0f)
        {
            _matchTimeRemaining = 0f;
            _matchEnded = true;
            BroadcastGameOver();
        }
    }

    private void BroadcastTimerSync()
    {
        PacketWriter w = new PacketWriter(MessageType.TimerSync);
        w.WriteFloat(Mathf.Max(0f, _matchTimeRemaining));
        BroadcastTcp(w.ToBytes());
    }

    private void SendTimerSync(Client c)
    {
        if (!_matchStarted) return;
        PacketWriter w = new PacketWriter(MessageType.TimerSync);
        w.WriteFloat(Mathf.Max(0f, _matchTimeRemaining));
        SendTcp(c, w.ToBytes());
    }

    private void BroadcastGameOver()
    {
        // Collect all players and sort by score descending
        List<Client> sorted = new List<Client>(_clients.Values);
        sorted.Sort((a, b) => b.Score.CompareTo(a.Score));

        int top = Mathf.Min(3, sorted.Count);
        PacketWriter w = new PacketWriter(MessageType.GameOver);
        w.WriteInt(top);
        for (int i = 0; i < top; i++)
        {
            w.WriteInt(sorted[i].Id);
            w.WriteString(sorted[i].State.PlayerName);
            w.WriteInt(sorted[i].Score);
        }
        BroadcastTcp(w.ToBytes());
    }
}
