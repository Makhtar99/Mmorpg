using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using TMPro;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class NetworkDespawnTests
{
    static readonly MethodInfo ServerUpdate =
        typeof(GameServer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

    static readonly MethodInfo ClientHandleTcp =
        typeof(GameClient).GetMethod("HandleTcp", BindingFlags.Instance | BindingFlags.NonPublic);

    GameObject serverObject;
    GameObject clientObject;
    GameObject launcherObject;
    GameObject prefabObject;
    GameObject menuObject;

    [TearDown]
    public void TearDown()
    {
        NetworkSceneLoader.Reset();
        NetworkSessionRequest.Clear();

        if (menuObject != null) UnityEngine.Object.DestroyImmediate(menuObject);
        if (launcherObject != null) UnityEngine.Object.DestroyImmediate(launcherObject);
        if (clientObject != null) UnityEngine.Object.DestroyImmediate(clientObject);
        if (prefabObject != null) UnityEngine.Object.DestroyImmediate(prefabObject);
        if (serverObject != null) UnityEngine.Object.DestroyImmediate(serverObject);
    }

    [Test]
    public void Server_BroadcastsDespawnWhenClientConnectionCloses()
    {
        int port = FindFreePort();
        GameServer server = CreateServer(port);
        using TcpClient firstClient = ConnectRawClient(port, character: 0);
        int firstId = ReadWelcome(firstClient, () => PumpServer(server));

        using TcpClient leavingClient = ConnectRawClient(port, character: 1);
        int leavingId = ReadWelcome(leavingClient, () => PumpServer(server));
        WaitForPacket(firstClient, MessageType.Spawn, () => PumpServer(server));

        leavingClient.Client.Shutdown(SocketShutdown.Both);
        leavingClient.Close();

        PacketReader despawn = WaitForPacket(
            firstClient,
            MessageType.Despawn,
            () => PumpServer(server));

        Assert.That(firstId, Is.EqualTo(1));
        Assert.That(leavingId, Is.EqualTo(2));
        Assert.That(despawn.ReadInt(), Is.EqualTo(leavingId));
    }

    [Test]
    public void GameClient_DestroysRemoteAvatarWhenDespawnArrives()
    {
        GameClient client = CreateGameClientWithPrefab();

        InvokeClientPacket(client, SpawnPacket(playerId: 7, character: 0, position: new Vector3(1f, 0f, 2f), yaw: 45f));

        Assert.That(client.RemotePlayerCount, Is.EqualTo(1));
        Assert.That(client.HasRemotePlayer(7), Is.True);
        NetworkPlayer remote = client.GetRemotePlayer(7);
        Assert.That(remote, Is.Not.Null);
        Assert.That(remote.gameObject.name, Is.EqualTo("Remote Player 7"));
        Assert.That(Vector3.Distance(remote.transform.position, new Vector3(1f, 0f, 2f)), Is.LessThan(0.01f));
        Assert.That(remote.transform.eulerAngles.y, Is.EqualTo(45f).Within(0.1f));

        InvokeClientPacket(client, DespawnPacket(7));

        Assert.That(client.RemotePlayerCount, Is.EqualTo(0));
        Assert.That(client.HasRemotePlayer(7), Is.False);
        Assert.That(remote == null, Is.True);
    }

    [Test]
    public void GameClient_LeaveServerClearsSpawnedPlayers()
    {
        GameClient client = CreateGameClientWithPrefab();

        InvokeClientPacket(client, WelcomePacket(playerId: 1));
        InvokeClientPacket(client, SpawnPacket(playerId: 7, character: 0));

        Assert.That(client.MyId, Is.EqualTo(1));
        Assert.That(client.HasLocalPlayer, Is.True);
        Assert.That(client.RemotePlayerCount, Is.EqualTo(1));

        client.LeaveServer();

        Assert.That(client.IsConnected, Is.False);
        Assert.That(client.MyId, Is.EqualTo(0));
        Assert.That(client.HasLocalPlayer, Is.False);
        Assert.That(client.RemotePlayerCount, Is.EqualTo(0));
    }

    [Test]
    public void NetworkLauncher_LeaveLoadsGameMenuScene()
    {
        launcherObject = new GameObject("launcher");
        clientObject = new GameObject("client");
        serverObject = new GameObject("server");
        string loadedScene = null;
        NetworkSceneLoader.LoadScene = sceneName => loadedScene = sceneName;

        NetworkLauncher launcher = launcherObject.AddComponent<NetworkLauncher>();
        launcher.Client = clientObject.AddComponent<GameClient>();
        launcher.Server = serverObject.AddComponent<GameServer>();
        launcher.GameMenuSceneName = "GameMenu";
        launcher.LoadGameMenuOnLeave = true;

        SetPrivateField(launcher, "_started", true);
        SetPrivateField(launcher, "_hosting", true);

        launcher.LeaveServer();

        Assert.That(loadedScene, Is.EqualTo("GameMenu"));
        Assert.That(launcher.IsSessionMenuOpen, Is.False);
        Assert.That(launcher.IsHosting, Is.False);
    }

    [Test]
    public void NetworkLauncher_ExplicitJoinIpOverridesSceneInputField()
    {
        launcherObject = new GameObject("launcher");
        clientObject = new GameObject("client");
        menuObject = new GameObject("ip input");

        NetworkLauncher launcher = launcherObject.AddComponent<NetworkLauncher>();
        launcher.Client = clientObject.AddComponent<GameClient>();
        launcher.IpInput = menuObject.AddComponent<TMP_InputField>();
        launcher.IpInput.text = "127.0.0.1";

        Assert.That(launcher.ResolveJoinServerIp("192.168.1.42"), Is.EqualTo("192.168.1.42"));
    }

    [Test]
    public void NetworkLauncher_CreatesSessionModalForLeavingServer()
    {
        launcherObject = new GameObject("launcher");
        clientObject = new GameObject("client");

        NetworkLauncher launcher = launcherObject.AddComponent<NetworkLauncher>();
        launcher.Client = clientObject.AddComponent<GameClient>();
        launcher.Client.ServerIp = "192.168.1.42";

        launcher.EnsureSessionMenu();

        Assert.That(launcher.SessionMenuPanel, Is.Not.Null);
        Assert.That(launcher.SessionMenuPanel.activeSelf, Is.False);

        launcher.ToggleSessionMenu();

        Button[] buttons = launcher.SessionMenuPanel.GetComponentsInChildren<Button>(true);
        string[] buttonLabels = buttons
            .Select(button => button.GetComponentInChildren<TMP_Text>(true)?.text)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToArray();

        Assert.That(launcher.IsSessionMenuOpen, Is.True);
        Assert.That(launcher.SessionMenuPanel.activeSelf, Is.True);
        Assert.That(launcher.SessionStatusText.text, Is.EqualTo("Connecté à 192.168.1.42"));
        Assert.That(buttonLabels, Does.Contain("Continuer"));
        Assert.That(buttonLabels, Does.Contain("Quitter le serveur"));

        launcher.CloseSessionMenu();

        Assert.That(launcher.IsSessionMenuOpen, Is.False);
        Assert.That(launcher.SessionMenuPanel.activeSelf, Is.False);
    }

    [Test]
    public void GameMenu_JoinQueuesJoinRequestAndLoadsGameplayScene()
    {
        menuObject = new GameObject("game menu");
        string loadedScene = null;
        NetworkSceneLoader.LoadScene = sceneName => loadedScene = sceneName;

        GameMenu menu = menuObject.AddComponent<GameMenu>();
        menu.GameSceneName = "MetaVerse";
        menu.ServerIp = "192.168.1.42";

        menu.Join();

        Assert.That(NetworkSessionRequest.Current.Mode, Is.EqualTo(NetworkSessionMode.Join));
        Assert.That(NetworkSessionRequest.Current.ServerIp, Is.EqualTo("192.168.1.42"));
        Assert.That(loadedScene, Is.EqualTo("MetaVerse"));
    }

    [Test]
    public void GameMenu_HostQueuesHostRequestAndLoadsGameplayScene()
    {
        menuObject = new GameObject("game menu");
        string loadedScene = null;
        NetworkSceneLoader.LoadScene = sceneName => loadedScene = sceneName;

        GameMenu menu = menuObject.AddComponent<GameMenu>();
        menu.GameSceneName = "MetaVerse";

        menu.Host();

        Assert.That(NetworkSessionRequest.Current.Mode, Is.EqualTo(NetworkSessionMode.Host));
        Assert.That(NetworkSessionRequest.Current.ServerIp, Is.EqualTo("127.0.0.1"));
        Assert.That(loadedScene, Is.EqualTo("MetaVerse"));
    }

    [Test]
    public void BuildSettings_StartWithGameMenuThenMetaVerse()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        Assert.That(File.Exists("Assets/Scenes/GameMenu.unity"), Is.True);
        Assert.That(scenes, Has.Length.GreaterThanOrEqualTo(2));
        Assert.That(scenes[0].enabled, Is.True);
        Assert.That(scenes[0].path, Is.EqualTo("Assets/Scenes/GameMenu.unity"));
        Assert.That(scenes[1].enabled, Is.True);
        Assert.That(scenes[1].path, Is.EqualTo("Assets/Demos/MetaVerse/MetaVerse.unity"));
    }

    [Test]
    public void GameMenuScene_UsesCanvasUiControls()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameMenu.unity", OpenSceneMode.Single);

        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        GameMenu menu = UnityEngine.Object.FindAnyObjectByType<GameMenu>(FindObjectsInactive.Include);
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
        string[] buttonLabels = buttons
            .Select(button => button.GetComponentInChildren<TMP_Text>(true)?.text)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToArray();

        Assert.That(canvas, Is.Not.Null);
        Assert.That(menu, Is.Not.Null);
        Assert.That(menu.ShowOnGuiMenu, Is.False);
        Assert.That(menu.IpInput, Is.Not.Null);
        Assert.That(menu.IpInput.text, Is.EqualTo("127.0.0.1"));
        Assert.That(menu.StatusText, Is.Not.Null);
        Assert.That(buttonLabels, Does.Contain("Héberger (serveur + jouer)"));
        Assert.That(buttonLabels, Does.Contain("Rejoindre"));
    }

    [Test]
    public void MetaVerseScene_DisablesLegacyNetworkOnGuiAndAutostartsMenuRequests()
    {
        EditorSceneManager.OpenScene("Assets/Demos/MetaVerse/MetaVerse.unity", OpenSceneMode.Single);

        NetworkLauncher launcher = UnityEngine.Object.FindAnyObjectByType<NetworkLauncher>(FindObjectsInactive.Include);

        Assert.That(launcher, Is.Not.Null);
        Assert.That(launcher.ShowOnGuiMenu, Is.False);
        Assert.That(launcher.GameMenuSceneName, Is.EqualTo("GameMenu"));
        Assert.That(launcher.LoadGameMenuOnLeave, Is.True);
        Assert.That(launcher.AutoStartPendingSession, Is.True);
    }

    GameServer CreateServer(int port)
    {
        serverObject = new GameObject("test server");
        GameServer server = serverObject.AddComponent<GameServer>();
        server.Port = port;
        Assert.That(server.StartServer(), Is.True);
        return server;
    }

    GameClient CreateGameClientWithPrefab()
    {
        clientObject = new GameObject("game client");
        GameClient client = clientObject.AddComponent<GameClient>();
        prefabObject = new GameObject("network player prefab");
        prefabObject.AddComponent<NetworkPlayer>();
        client.CharacterPrefabs = new[] { prefabObject };
        return client;
    }

    static TcpClient ConnectRawClient(int port, byte character)
    {
        var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        client.ReceiveTimeout = 1000;

        var writer = new PacketWriter(MessageType.Connect);
        writer.WriteByte(character);
        byte[] bytes = writer.ToBytes();
        client.GetStream().Write(bytes, 0, bytes.Length);

        return client;
    }

    static int ReadWelcome(TcpClient client, Action tick)
    {
        PacketReader welcome = WaitForPacket(client, MessageType.Welcome, tick);
        return welcome.ReadInt();
    }

    static PacketReader WaitForPacket(
        TcpClient client,
        MessageType expectedType,
        Action tick = null)
    {
        var framer = new PacketFramer();
        byte[] buffer = new byte[1024];
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);

        while (DateTime.UtcNow < deadline)
        {
            tick?.Invoke();

            while (client.Available > 0)
            {
                int read = client.GetStream().Read(buffer, 0, Math.Min(buffer.Length, client.Available));
                framer.Push(buffer, read);
            }

            while (framer.TryRead(out byte[] packet))
            {
                var reader = new PacketReader(packet);
                if (reader.Type == expectedType)
                {
                    return reader;
                }
            }

            Thread.Sleep(10);
        }

        Assert.Fail($"Timed out waiting for {expectedType} packet.");
        return null;
    }

    static void PumpServer(GameServer server)
    {
        ServerUpdate.Invoke(server, null);
    }

    static void InvokeClientPacket(GameClient client, byte[] packet)
    {
        ClientHandleTcp.Invoke(client, new object[] { packet });
    }

    static byte[] WelcomePacket(int playerId)
    {
        var writer = new PacketWriter(MessageType.Welcome);
        writer.WriteInt(playerId);
        return writer.ToBytes();
    }

    static byte[] SpawnPacket(int playerId, byte character)
    {
        return SpawnPacket(playerId, character, Vector3.zero, 0f);
    }

    static byte[] SpawnPacket(int playerId, byte character, Vector3 position, float yaw)
    {
        var writer = new PacketWriter(MessageType.Spawn);
        writer.WritePlayerState(
            new PlayerState
            {
                Id = playerId,
                Character = character,
                Position = position,
                Yaw = yaw,
            });
        return writer.ToBytes();
    }

    static byte[] DespawnPacket(int playerId)
    {
        var writer = new PacketWriter(MessageType.Despawn);
        writer.WriteInt(playerId);
        return writer.ToBytes();
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    static int FindFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint).Port;
    }
}
