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

    static readonly MethodInfo GameMenuStart =
        typeof(GameMenu).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);

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
    public void GameClient_SetsRemotePlayerNameWhenSpawnArrives()
    {
        GameClient client = CreateGameClientWithPrefab();

        InvokeClientPacket(client, SpawnPacket(playerId: 7, character: 0, playerName: "Samy"));

        NetworkPlayer remote = client.GetRemotePlayer(7);
        CharacterScore score = remote.GetComponentInChildren<CharacterScore>();

        Assert.That(score, Is.Not.Null);
        Assert.That(score.PlayerName, Is.EqualTo("Samy"));
        Assert.That(score.TxtName, Is.Not.Null);
        Assert.That(score.TxtName.text, Is.EqualTo("Samy"));
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
        menu.SelectedCharacter = 2;
        menu.PlayerName = "Samy";

        menu.Join();

        Assert.That(NetworkSessionRequest.Current.Mode, Is.EqualTo(NetworkSessionMode.Join));
        Assert.That(NetworkSessionRequest.Current.ServerIp, Is.EqualTo("192.168.1.42"));
        Assert.That(NetworkSessionRequest.Current.Character, Is.EqualTo((byte)2));
        Assert.That(NetworkSessionRequest.Current.PlayerName, Is.EqualTo("Samy"));
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
        menu.SelectedCharacter = 4;
        menu.PlayerName = "HostName";

        menu.Host();

        Assert.That(NetworkSessionRequest.Current.Mode, Is.EqualTo(NetworkSessionMode.Host));
        Assert.That(NetworkSessionRequest.Current.ServerIp, Is.EqualTo("127.0.0.1"));
        Assert.That(NetworkSessionRequest.Current.Character, Is.EqualTo((byte)4));
        Assert.That(NetworkSessionRequest.Current.PlayerName, Is.EqualTo("HostName"));
        Assert.That(loadedScene, Is.EqualTo("MetaVerse"));
    }

    [Test]
    public void NetworkSessionRequest_StoresSelectedCharacterAndPlayerNameForHostAndJoin()
    {
        NetworkSessionRequest.Host(1, " Samy ");

        Assert.That(NetworkSessionRequest.Current.Mode, Is.EqualTo(NetworkSessionMode.Host));
        Assert.That(NetworkSessionRequest.Current.Character, Is.EqualTo((byte)1));
        Assert.That(NetworkSessionRequest.Current.PlayerName, Is.EqualTo("Samy"));

        NetworkSessionRequest.Join(" 10.0.0.8 ", 5, "");

        Assert.That(NetworkSessionRequest.Current.Mode, Is.EqualTo(NetworkSessionMode.Join));
        Assert.That(NetworkSessionRequest.Current.ServerIp, Is.EqualTo("10.0.0.8"));
        Assert.That(NetworkSessionRequest.Current.Character, Is.EqualTo((byte)5));
        Assert.That(NetworkSessionRequest.Current.PlayerName, Is.EqualTo("Player"));
    }

    [Test]
    public void NetworkLauncher_AppliesPendingSessionCharacterToClient()
    {
        launcherObject = new GameObject("network launcher");
        GameClient client = launcherObject.AddComponent<GameClient>();
        NetworkLauncher launcher = launcherObject.AddComponent<NetworkLauncher>();
        launcher.Client = client;

        launcher.ApplySessionRequest(new NetworkSessionRequestData(NetworkSessionMode.Join, "10.0.0.8", 3, "Samy"));

        Assert.That(client.ServerIp, Is.EqualTo("10.0.0.8"));
        Assert.That(client.Character, Is.EqualTo((byte)3));
        Assert.That(client.PlayerName, Is.EqualTo("Samy"));
    }

    [Test]
    public void GameMenu_UsesPlayerNameInputWhenQueueingSession()
    {
        menuObject = new GameObject("game menu");
        string loadedScene = null;
        NetworkSceneLoader.LoadScene = sceneName => loadedScene = sceneName;

        GameMenu menu = menuObject.AddComponent<GameMenu>();
        GameObject inputObject = new GameObject("player name input");
        inputObject.transform.SetParent(menuObject.transform, false);
        menu.PlayerNameInput = inputObject.AddComponent<TMP_InputField>();
        menu.PlayerNameInput.text = "Nora";

        menu.Join();

        Assert.That(NetworkSessionRequest.Current.PlayerName, Is.EqualTo("Nora"));
        Assert.That(loadedScene, Is.EqualTo("MetaVerse"));
    }

    [Test]
    public void CharacterScore_SetPlayerNameCreatesNameTextBelowScore()
    {
        menuObject = new GameObject("score");
        CharacterScore score = menuObject.AddComponent<CharacterScore>();
        GameObject scoreTextObject = new GameObject(
            "Score Text",
            typeof(RectTransform),
            typeof(MeshRenderer),
            typeof(TextMeshPro));
        scoreTextObject.transform.SetParent(menuObject.transform, false);
        RectTransform scoreTextRect = scoreTextObject.GetComponent<RectTransform>();
        scoreTextRect.localPosition = new Vector3(0f, 3f, 0f);
        scoreTextRect.localScale = Vector3.one * 0.1f;
        score.TxtScore = scoreTextObject.GetComponent<TMP_Text>();

        score.SetPlayerName("Samy");

        Assert.That(score.PlayerName, Is.EqualTo("Samy"));
        Assert.That(score.TxtName, Is.Not.Null);
        Assert.That(score.TxtName.text, Is.EqualTo("Samy"));

        RectTransform nameRect = score.TxtName.GetComponent<RectTransform>();
        Assert.That(nameRect.localPosition.y, Is.LessThan(scoreTextRect.localPosition.y));
        Assert.That(nameRect.localPosition.y, Is.GreaterThan(scoreTextRect.localPosition.y - 0.8f));
        Assert.That(nameRect.localScale.x, Is.EqualTo(scoreTextRect.localScale.x).Within(0.001f));
    }

    [Test]
    public void Protocol_PlayerStateRoundTripsPlayerName()
    {
        var writer = new PacketWriter(MessageType.Spawn);
        writer.WritePlayerState(
            new PlayerState
            {
                Id = 9,
                Character = 2,
                PlayerName = "Samy",
                Position = new Vector3(1f, 2f, 3f),
                Yaw = 45f,
            });

        var reader = new PacketReader(writer.ToBytes());
        PlayerState state = reader.ReadPlayerState();

        Assert.That(state.Id, Is.EqualTo(9));
        Assert.That(state.Character, Is.EqualTo((byte)2));
        Assert.That(state.PlayerName, Is.EqualTo("Samy"));
        Assert.That(state.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        Assert.That(state.Yaw, Is.EqualTo(45f));
    }

    [Test]
    public void Server_SendsExistingPlayerNameToJoiningClient()
    {
        int port = FindFreePort();
        GameServer server = CreateServer(port);
        using TcpClient firstClient = ConnectRawClient(port, character: 0, playerName: "Samy");
        ReadWelcome(firstClient, () => PumpServer(server));

        using TcpClient secondClient = ConnectRawClient(port, character: 1, playerName: "Nora");
        ReadWelcome(secondClient, () => PumpServer(server));

        PacketReader spawn = WaitForPacket(secondClient, MessageType.Spawn, () => PumpServer(server));
        PlayerState existingPlayer = spawn.ReadPlayerState();

        Assert.That(existingPlayer.Id, Is.EqualTo(1));
        Assert.That(existingPlayer.PlayerName, Is.EqualTo("Samy"));
    }

    [Test]
    public void GameMenu_SelectCharacterClampsToAvailableChoices()
    {
        menuObject = new GameObject("game menu");
        GameMenu menu = menuObject.AddComponent<GameMenu>();

        menu.SelectCharacter(99);

        Assert.That(menu.SelectedCharacter, Is.EqualTo((byte)(menu.CharacterNames.Length - 1)));

        menu.SelectCharacter(-4);

        Assert.That(menu.SelectedCharacter, Is.EqualTo((byte)0));
    }

    [Test]
    public void GameMenu_NextAndPreviousCharacterWrapAround()
    {
        menuObject = new GameObject("game menu");
        GameMenu menu = menuObject.AddComponent<GameMenu>();

        menu.SelectCharacter(0);
        menu.NextCharacter();

        Assert.That(menu.SelectedCharacter, Is.EqualTo((byte)1));

        menu.PreviousCharacter();

        Assert.That(menu.SelectedCharacter, Is.EqualTo((byte)0));

        menu.PreviousCharacter();

        Assert.That(menu.SelectedCharacter, Is.EqualTo((byte)(menu.CharacterNames.Length - 1)));
    }

    [Test]
    public void GameMenu_DefaultCharacterNamesMatchMetaVersePrefabOrder()
    {
        menuObject = new GameObject("game menu");
        GameMenu menu = menuObject.AddComponent<GameMenu>();

        Assert.That(
            menu.CharacterNames,
            Is.EqualTo(new[] { "Barbare", "Ingenieur", "Druide", "Chevalier", "Mage", "Rogue" }));
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
    public void GameMenu_CreatesCharacterCarouselControlsInCanvas()
    {
        menuObject = new GameObject("game menu", typeof(Canvas));
        GameObject panel = new GameObject("Connection Panel", typeof(RectTransform));
        panel.transform.SetParent(menuObject.transform, false);
        GameMenu menu = menuObject.AddComponent<GameMenu>();
        menu.ShowOnGuiMenu = false;
        GameObject firstPreview = new GameObject("barbarian preview");
        GameObject secondPreview = new GameObject("engineer preview");
        menu.CharacterNames = new[] { "Barbare", "Ingenieur" };
        menu.CharacterPreviewPrefabs = new[] { firstPreview, secondPreview };

        menu.EnsureCharacterSelectionUi();

        Button[] buttons = menuObject.GetComponentsInChildren<Button>(true);
        string[] buttonLabels = buttons
            .Select(button => button.GetComponentInChildren<TMP_Text>(true)?.text)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToArray();

        Assert.That(buttonLabels, Does.Contain("<"));
        Assert.That(buttonLabels, Does.Contain(">"));
        Assert.That(menu.CharacterNameText, Is.Not.Null);
        Assert.That(menu.CharacterNameText.text, Is.EqualTo("Barbare"));
        Assert.That(menu.CharacterPreviewRoot, Is.Not.Null);
        Assert.That(menu.CharacterPreviewRoot.childCount, Is.EqualTo(1));
        Assert.That(menu.CharacterPreviewRoot.GetChild(0).name, Does.StartWith("barbarian preview"));

        menu.NextCharacter();

        Assert.That(menu.CharacterNameText.text, Is.EqualTo("Ingenieur"));
        Assert.That(menu.CharacterPreviewRoot.childCount, Is.EqualTo(1));
        Assert.That(menu.CharacterPreviewRoot.GetChild(0).name, Does.StartWith("engineer preview"));

        UnityEngine.Object.DestroyImmediate(firstPreview);
        UnityEngine.Object.DestroyImmediate(secondPreview);
    }

    [Test]
    public void GameMenu_StylesCanvasBackgroundAsStaticMenuBackdrop()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        menuObject = new GameObject("game menu", typeof(Canvas));
        GameObject background = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        background.transform.SetParent(menuObject.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = new Color(0.09f, 0.13f, 0.18f, 1f);
        backgroundImage.raycastTarget = true;

        GameObject panel = new GameObject("Connection Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(menuObject.transform, false);
        GameMenu menu = menuObject.AddComponent<GameMenu>();
        menu.ShowOnGuiMenu = false;

        menu.EnsureCharacterSelectionUi();

        Assert.That(backgroundImage.color.r, Is.EqualTo(0.10f).Within(0.001f));
        Assert.That(backgroundImage.color.g, Is.EqualTo(0.11f).Within(0.001f));
        Assert.That(backgroundImage.color.b, Is.EqualTo(0.10f).Within(0.001f));
        Assert.That(backgroundImage.color.a, Is.EqualTo(1f));
        Assert.That(backgroundImage.raycastTarget, Is.False);
    }

    [Test]
    public void GameMenu_StartDoesNotCreateFakeWorldBackdrop()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        menuObject = new GameObject("game menu", typeof(Canvas));
        GameObject panel = new GameObject("Connection Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(menuObject.transform, false);
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        GameMenu menu = menuObject.AddComponent<GameMenu>();
        menu.ShowOnGuiMenu = false;

        GameMenuStart.Invoke(menu, null);

        Assert.That(GameObject.Find("Menu Background"), Is.Null);
        Assert.That(GameObject.Find("Road"), Is.Null);
        Assert.That(GameObject.Find("Left Sidewalk"), Is.Null);
        Assert.That(GameObject.Find("Right Sidewalk"), Is.Null);
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

    static TcpClient ConnectRawClient(int port, byte character, string playerName = "Player")
    {
        var client = new TcpClient();
        client.Connect(IPAddress.Loopback, port);
        client.ReceiveTimeout = 1000;

        var writer = new PacketWriter(MessageType.Connect);
        writer.WriteByte(character);
        writer.WriteString(playerName);
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

    static byte[] SpawnPacket(int playerId, byte character, string playerName)
    {
        return SpawnPacket(playerId, character, Vector3.zero, 0f, playerName);
    }

    static byte[] SpawnPacket(int playerId, byte character, Vector3 position, float yaw)
    {
        return SpawnPacket(playerId, character, position, yaw, "Player");
    }

    static byte[] SpawnPacket(int playerId, byte character, Vector3 position, float yaw, string playerName)
    {
        var writer = new PacketWriter(MessageType.Spawn);
        writer.WritePlayerState(
            new PlayerState
            {
                Id = playerId,
                Character = character,
                PlayerName = playerName,
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
