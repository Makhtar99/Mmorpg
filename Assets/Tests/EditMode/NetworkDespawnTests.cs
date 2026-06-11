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
    GameObject bonusObject;
    GameObject secondBonusObject;
    GameObject terrainObject;

    [TearDown]
    public void TearDown()
    {
        NetworkSceneLoader.Reset();
        NetworkSessionRequest.Clear();

        if (secondBonusObject != null) UnityEngine.Object.DestroyImmediate(secondBonusObject);
        if (bonusObject != null) UnityEngine.Object.DestroyImmediate(bonusObject);
        if (terrainObject != null) UnityEngine.Object.DestroyImmediate(terrainObject);
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
    public void GameClient_ScoreboardUpdatesLocalPlayerWhenScoreChanges()
    {
        GameClient client = CreateGameClientWithPrefab();
        client.PlayerName = "Samy";

        InvokeClientPacket(client, WelcomePacket(playerId: 1));
        InvokeClientPacket(client, PickupAckPacket(bonusId: 3, playerId: 1, score: 5));

        Assert.That(client.Scoreboard, Is.Not.Null);
        Assert.That(client.Scoreboard.DisplayText.text, Does.Contain("Samy 5"));
    }

    [Test]
    public void GameClient_ScoreboardTracksRemoteSpawnAndDespawn()
    {
        GameClient client = CreateGameClientWithPrefab();

        InvokeClientPacket(client, SpawnPacket(playerId: 7, character: 0, playerName: "Nora", score: 4));

        Assert.That(client.Scoreboard.DisplayText.text, Does.Contain("Nora 4"));

        InvokeClientPacket(client, DespawnPacket(7));

        Assert.That(client.Scoreboard.DisplayText.text, Does.Not.Contain("Nora 4"));
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
                Score = 12,
                Position = new Vector3(1f, 2f, 3f),
                Yaw = 45f,
            });

        var reader = new PacketReader(writer.ToBytes());
        PlayerState state = reader.ReadPlayerState();

        Assert.That(state.Id, Is.EqualTo(9));
        Assert.That(state.Character, Is.EqualTo((byte)2));
        Assert.That(state.PlayerName, Is.EqualTo("Samy"));
        Assert.That(state.Score, Is.EqualTo(12));
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
    public void Server_SendsExistingPlayerScoreToJoiningClient()
    {
        int port = FindFreePort();
        GameServer server = CreateServer(
            port,
            server =>
            {
                server.TargetActiveBonusCount = 1;
                server.MinimumActiveBonusCount = 0;
                server.BonusSpawnAreaCenter = new Vector3(100f, 0f, 200f);
                server.BonusSpawnAreaSize = new Vector2(30f, 40f);
                server.BonusSpawnGroundLayers = 0;
            });
        using TcpClient firstClient = ConnectRawClient(port, character: 0, playerName: "Samy");
        ReadWelcome(firstClient, () => PumpServer(server));

        SendPickupRequest(firstClient, bonusId: 0);
        WaitForPacket(firstClient, MessageType.PickupAck, () => PumpServer(server));

        using TcpClient secondClient = ConnectRawClient(port, character: 1, playerName: "Nora");
        ReadWelcome(secondClient, () => PumpServer(server));

        PacketReader spawn = WaitForPacket(secondClient, MessageType.Spawn, () => PumpServer(server));
        PlayerState existingPlayer = spawn.ReadPlayerState();

        Assert.That(existingPlayer.Id, Is.EqualTo(1));
        Assert.That(existingPlayer.PlayerName, Is.EqualTo("Samy"));
        Assert.That(existingPlayer.Score, Is.EqualTo(1));
    }

    [Test]
    public void NetworkScoreboard_RendersPlayersSortedByScore()
    {
        menuObject = new GameObject("scoreboard");
        NetworkScoreboard scoreboard = menuObject.AddComponent<NetworkScoreboard>();

        scoreboard.UpsertPlayer(1, "Samy", 2);
        scoreboard.UpsertPlayer(2, "Nora", 7);
        scoreboard.UpsertPlayer(3, "Alex", 4);

        string rendered = scoreboard.DisplayText.text;

        Assert.That(rendered.IndexOf("Nora 7", StringComparison.Ordinal), Is.LessThan(rendered.IndexOf("Alex 4", StringComparison.Ordinal)));
        Assert.That(rendered.IndexOf("Alex 4", StringComparison.Ordinal), Is.LessThan(rendered.IndexOf("Samy 2", StringComparison.Ordinal)));
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

    [Test]
    public void MetaVerseScene_SerializesDynamicBonusPoolSettings()
    {
        string sceneYaml = File.ReadAllText("Assets/Demos/MetaVerse/MetaVerse.unity");

        Assert.That(sceneYaml, Does.Contain("TargetActiveBonusCount: 500"));
        Assert.That(sceneYaml, Does.Contain("MinimumActiveBonusCount: 500"));
        Assert.That(sceneYaml, Does.Contain("RandomizeInitialBonusPositions: 1"));
        Assert.That(sceneYaml, Does.Contain("BonusSpawnAreaCenter: {x: 240.5, y: 0, z: 254.2}"));
        Assert.That(sceneYaml, Does.Contain("BonusSpawnAreaSize: {x: 345, y: 312}"));
    }

    [Test]
    public void CharacterMovementSpeedUsesPlayableIntermediateValue()
    {
        var networkPlayerObject = new GameObject("network player");
        NetworkPlayer networkPlayer = networkPlayerObject.AddComponent<NetworkPlayer>();
        menuObject = networkPlayerObject;

        Assert.That(networkPlayer.MoveSpeed, Is.EqualTo(4.2f).Within(0.001f));
        Assert.That(networkPlayer.RotateSpeed, Is.EqualTo(300f).Within(0.001f));

        string[] prefabPaths =
        {
            "Assets/Demos/MetaVerse/Prefabs/barbarian.prefab",
            "Assets/Demos/MetaVerse/Prefabs/engineer.prefab",
            "Assets/Demos/MetaVerse/Prefabs/druid.prefab",
            "Assets/Demos/MetaVerse/Prefabs/knight.prefab",
            "Assets/Demos/MetaVerse/Prefabs/mage.prefab",
            "Assets/Demos/MetaVerse/Prefabs/rogue.prefab",
        };

        foreach (string prefabPath in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            CharacterController controller = prefab.GetComponent<CharacterController>();

            Assert.That(prefab, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.WalkSpeed, Is.EqualTo(4.2f).Within(0.001f));
            Assert.That(controller.RotateSpeed, Is.EqualTo(300f).Within(0.001f));
        }
    }

    [Test]
    public void GameClient_DeactivatesBonusWhenPickupAckArrives()
    {
        GameClient client = CreateGameClientWithPrefab();
        Bonus bonus = CreateRegisteredBonus(client, 3, new Vector3(1f, 0f, 2f));

        InvokeClientPacket(client, PickupAckPacket(bonusId: 3, playerId: 1, score: 1));

        Assert.That(bonus == null, Is.False);
        Assert.That(bonus.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void GameClient_RespawnsBonusWhenBonusSpawnArrives()
    {
        GameClient client = CreateGameClientWithPrefab();
        Bonus bonus = CreateRegisteredBonus(client, 4, Vector3.zero);
        bonus.gameObject.SetActive(false);

        InvokeClientPacket(client, BonusSpawnPacket(4, new Vector3(5f, 0f, 6f)));

        Assert.That(bonus.gameObject.activeSelf, Is.True);
        Assert.That(Vector3.Distance(bonus.transform.position, new Vector3(5f, 0f, 6f)), Is.LessThan(0.01f));
    }

    [Test]
    public void GameClient_AppliesBonusStateWithActiveFlagsAndPositions()
    {
        GameClient client = CreateGameClientWithPrefab();
        Bonus inactiveBonus = CreateRegisteredBonus(client, 5, Vector3.zero);
        Bonus activeBonus = CreateRegisteredBonus(client, 6, Vector3.zero, useSecondObject: true);

        InvokeClientPacket(
            client,
            BonusStatePacket(
                (5, false, new Vector3(1f, 0f, 1f)),
                (6, true, new Vector3(2f, 0f, 2f))));

        Assert.That(inactiveBonus.gameObject.activeSelf, Is.False);
        Assert.That(activeBonus.gameObject.activeSelf, Is.True);
        Assert.That(Vector3.Distance(activeBonus.transform.position, new Vector3(2f, 0f, 2f)), Is.LessThan(0.01f));
    }

    [Test]
    public void GameClient_CreatesMissingBonusObjectsFromRegisteredPrototype()
    {
        GameClient client = CreateGameClientWithPrefab();
        CreateRegisteredBonus(client, 0, Vector3.zero);

        InvokeClientPacket(
            client,
            BonusStatePacket(
                (0, true, new Vector3(1f, 0f, 1f)),
                (7, true, new Vector3(12f, 0f, 18f)),
                (8, false, new Vector3(14f, 0f, 20f))));

        Bonus clonedActiveBonus = client.GetBonus(7);
        Bonus clonedInactiveBonus = client.GetBonus(8);

        Assert.That(client.RegisteredBonusCount, Is.EqualTo(3));
        Assert.That(clonedActiveBonus, Is.Not.Null);
        Assert.That(clonedActiveBonus.gameObject.activeSelf, Is.True);
        Assert.That(Vector3.Distance(clonedActiveBonus.transform.position, new Vector3(12f, 0f, 18f)), Is.LessThan(0.01f));
        Assert.That(clonedInactiveBonus, Is.Not.Null);
        Assert.That(clonedInactiveBonus.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void GameClient_CreatesLargeBonusPoolFromSingleRegisteredPrototype()
    {
        GameClient client = CreateGameClientWithPrefab();
        Bonus prototype = CreateRegisteredBonus(client, 0, Vector3.zero);
        prototype.transform.SetParent(clientObject.transform);

        var states = new (int id, bool active, Vector3 position)[500];
        for (int i = 0; i < states.Length; i++)
        {
            states[i] = (i, true, new Vector3(i, 0.25f, i + 1f));
        }

        InvokeClientPacket(client, BonusStatePacket(states));

        Assert.That(client.RegisteredBonusCount, Is.EqualTo(500));
        Assert.That(client.GetBonus(499), Is.Not.Null);
        Assert.That(client.GetBonus(499).gameObject.activeSelf, Is.True);
        Assert.That(Vector3.Distance(client.GetBonus(499).transform.position, new Vector3(499f, 0.25f, 500f)), Is.LessThan(0.01f));
    }

    [Test]
    public void Server_BroadcastsBonusSpawnAfterRespawnDelay()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        int port = FindFreePort();
        GameServer server = CreateServer(
            port,
            server =>
            {
                server.BonusRespawnDelaySeconds = 0f;
                server.TargetActiveBonusCount = 0;
                server.MinimumActiveBonusCount = 0;
                server.BonusSpawnAreaCenter = new Vector3(100f, 0f, 200f);
                server.BonusSpawnAreaSize = new Vector2(30f, 40f);
                server.BonusSpawnHeightOffset = 0.25f;
                server.BonusSpawnGroundLayers = 0;
            });

        using TcpClient firstClient = ConnectRawClient(port, character: 0);
        ReadWelcome(firstClient, () => PumpServer(server));
        using TcpClient secondClient = ConnectRawClient(port, character: 1);
        ReadWelcome(secondClient, () => PumpServer(server));

        SendPickupRequest(firstClient, bonusId: 42);

        PacketReader bonusSpawn = WaitForPacket(secondClient, MessageType.BonusSpawn, () => PumpServer(server));

        Assert.That(bonusSpawn.ReadInt(), Is.EqualTo(42));
        AssertPositionInsideArea(
            bonusSpawn.ReadVector3(),
            new Vector3(100f, 0f, 200f),
            new Vector2(30f, 40f),
            0.25f);
    }

    [Test]
    public void Server_SendsConfiguredActiveBonusPoolInsideMapArea()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        int port = FindFreePort();
        GameServer server = CreateServer(
            port,
            server =>
            {
                server.TargetActiveBonusCount = 12;
                server.MinimumActiveBonusCount = 0;
                server.BonusSpawnAreaCenter = new Vector3(100f, 0f, 200f);
                server.BonusSpawnAreaSize = new Vector2(30f, 40f);
                server.BonusSpawnHeightOffset = 0.25f;
                server.BonusSpawnGroundLayers = 0;
            });

        using TcpClient client = ConnectRawClient(port, character: 0);
        ReadWelcome(client, () => PumpServer(server));

        PacketReader bonusState = WaitForPacket(client, MessageType.BonusState, () => PumpServer(server));
        int count = bonusState.ReadInt();

        Assert.That(count, Is.EqualTo(12));
        for (int i = 0; i < count; i++)
        {
            bonusState.ReadInt();
            bool active = bonusState.ReadByte() != 0;
            Vector3 position = bonusState.ReadVector3();

            Assert.That(active, Is.True);
            AssertPositionInsideArea(position, new Vector3(100f, 0f, 200f), new Vector2(30f, 40f), 0.25f);
        }
    }

    [Test]
    public void Server_EnforcesMinimumBonusPoolWhenSerializedTargetIsTooLow()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        int port = FindFreePort();
        GameServer server = CreateServer(
            port,
            server =>
            {
                server.TargetActiveBonusCount = 15;
                server.MinimumActiveBonusCount = 500;
                server.BonusSpawnAreaCenter = new Vector3(100f, 0f, 200f);
                server.BonusSpawnAreaSize = new Vector2(200f, 200f);
                server.BonusSpawnHeightOffset = 0.25f;
                server.BonusSpawnGroundLayers = 0;
            });

        using TcpClient client = ConnectRawClient(port, character: 0);
        ReadWelcome(client, () => PumpServer(server));

        PacketReader bonusState = WaitForPacket(client, MessageType.BonusState, () => PumpServer(server));

        Assert.That(bonusState.ReadInt(), Is.EqualTo(500));
    }

    [Test]
    public void Server_RandomizesSceneBonusInitialPositionsInsideMapArea()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateRegisteredSceneBonus(0, new Vector3(1f, 0f, 1f));
        int port = FindFreePort();
        GameServer server = CreateServer(
            port,
            server =>
            {
                server.TargetActiveBonusCount = 1;
                server.MinimumActiveBonusCount = 0;
                server.RandomizeInitialBonusPositions = true;
                server.BonusSpawnAreaCenter = new Vector3(100f, 0f, 200f);
                server.BonusSpawnAreaSize = new Vector2(30f, 40f);
                server.BonusSpawnHeightOffset = 0.25f;
                server.BonusSpawnGroundLayers = 0;
            });

        using TcpClient client = ConnectRawClient(port, character: 0);
        ReadWelcome(client, () => PumpServer(server));

        PacketReader bonusState = WaitForPacket(client, MessageType.BonusState, () => PumpServer(server));
        int count = bonusState.ReadInt();
        int bonusId = bonusState.ReadInt();
        bool active = bonusState.ReadByte() != 0;
        Vector3 position = bonusState.ReadVector3();

        Assert.That(count, Is.EqualTo(1));
        Assert.That(bonusId, Is.EqualTo(0));
        Assert.That(active, Is.True);
        AssertPositionInsideArea(position, new Vector3(100f, 0f, 200f), new Vector2(30f, 40f), 0.25f);
        Assert.That(Vector3.Distance(position, new Vector3(1f, 0f, 1f)), Is.GreaterThan(1f));
    }

    [Test]
    public void Server_UsesTerrainBoundsWhenSpawnAreaIsNotConfigured()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        TerrainData terrainData = new TerrainData
        {
            heightmapResolution = 33,
            size = new Vector3(60f, 1f, 80f),
        };
        terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainObject.transform.position = new Vector3(10f, 0f, 20f);

        int port = FindFreePort();
        GameServer server = CreateServer(
            port,
            server =>
            {
                server.TargetActiveBonusCount = 5;
                server.MinimumActiveBonusCount = 0;
                server.BonusSpawnAreaSize = Vector2.zero;
                server.BonusSpawnGroundLayers = 0;
            });

        using TcpClient client = ConnectRawClient(port, character: 0);
        ReadWelcome(client, () => PumpServer(server));

        PacketReader bonusState = WaitForPacket(client, MessageType.BonusState, () => PumpServer(server));
        int count = bonusState.ReadInt();

        Assert.That(count, Is.EqualTo(5));
        for (int i = 0; i < count; i++)
        {
            bonusState.ReadInt();
            bonusState.ReadByte();
            Vector3 position = bonusState.ReadVector3();

            AssertPositionInsideArea(position, new Vector3(40f, 0f, 60f), new Vector2(60f, 80f), 0.28f);
        }
    }

    GameServer CreateServer(int port, Action<GameServer> configure = null)
    {
        serverObject = new GameObject("test server");
        GameServer server = serverObject.AddComponent<GameServer>();
        server.Port = port;
        configure?.Invoke(server);
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

    Bonus CreateRegisteredBonus(GameClient client, int bonusId, Vector3 position, bool useSecondObject = false)
    {
        GameObject target = new GameObject("bonus " + bonusId);
        target.transform.position = position;

        if (useSecondObject)
        {
            secondBonusObject = target;
        }
        else
        {
            bonusObject = target;
        }

        Bonus bonus = target.AddComponent<Bonus>();
        bonus.BonusId = bonusId;
        client.RegisterBonus(bonus);
        return bonus;
    }

    Bonus CreateRegisteredSceneBonus(int bonusId, Vector3 position)
    {
        bonusObject = new GameObject("scene bonus " + bonusId);
        bonusObject.transform.position = position;
        Bonus bonus = bonusObject.AddComponent<Bonus>();
        bonus.BonusId = bonusId;
        return bonus;
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

    static void SendPickupRequest(TcpClient client, int bonusId)
    {
        var writer = new PacketWriter(MessageType.PickupRequest);
        writer.WriteInt(bonusId);
        byte[] bytes = writer.ToBytes();
        client.GetStream().Write(bytes, 0, bytes.Length);
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

    static void AssertPositionInsideArea(Vector3 position, Vector3 center, Vector2 size, float expectedY)
    {
        Assert.That(position.x, Is.InRange(center.x - size.x * 0.5f, center.x + size.x * 0.5f));
        Assert.That(position.z, Is.InRange(center.z - size.y * 0.5f, center.z + size.y * 0.5f));
        Assert.That(position.y, Is.EqualTo(expectedY).Within(0.01f));
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

    static byte[] SpawnPacket(int playerId, byte character, string playerName, int score)
    {
        return SpawnPacket(playerId, character, Vector3.zero, 0f, playerName, score);
    }

    static byte[] SpawnPacket(int playerId, byte character, Vector3 position, float yaw)
    {
        return SpawnPacket(playerId, character, position, yaw, "Player");
    }

    static byte[] SpawnPacket(int playerId, byte character, Vector3 position, float yaw, string playerName)
    {
        return SpawnPacket(playerId, character, position, yaw, playerName, 0);
    }

    static byte[] SpawnPacket(int playerId, byte character, Vector3 position, float yaw, string playerName, int score)
    {
        var writer = new PacketWriter(MessageType.Spawn);
        writer.WritePlayerState(
            new PlayerState
            {
                Id = playerId,
                Character = character,
                PlayerName = playerName,
                Score = score,
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

    static byte[] PickupAckPacket(int bonusId, int playerId, int score)
    {
        var writer = new PacketWriter(MessageType.PickupAck);
        writer.WriteInt(bonusId);
        writer.WriteInt(playerId);
        writer.WriteInt(score);
        return writer.ToBytes();
    }

    static byte[] BonusSpawnPacket(int bonusId, Vector3 position)
    {
        var writer = new PacketWriter(MessageType.BonusSpawn);
        writer.WriteInt(bonusId);
        writer.WriteVector3(position);
        return writer.ToBytes();
    }

    static byte[] BonusStatePacket(params (int id, bool active, Vector3 position)[] states)
    {
        var writer = new PacketWriter(MessageType.BonusState);
        writer.WriteInt(states.Length);
        foreach ((int id, bool active, Vector3 position) in states)
        {
            writer.WriteInt(id);
            writer.WriteByte(active ? (byte)1 : (byte)0);
            writer.WriteVector3(position);
        }
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
