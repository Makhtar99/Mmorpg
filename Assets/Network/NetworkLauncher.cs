using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class NetworkLauncher : MonoBehaviour
{
    public GameServer Server;
    public GameClient Client;

    public bool ShowOnGuiMenu = true;

    public TMPro.TMP_InputField IpInput;
    public TMPro.TMP_Text StatusText;
    public GameObject MenuPanel;
    public GameObject SessionMenuPanel;
    public TMPro.TMP_Text SessionStatusText;

    public string GameMenuSceneName = "GameMenu";
    public bool LoadGameMenuOnLeave = true;
    public bool AutoStartPendingSession = true;

    private string _ip = "127.0.0.1";
    private bool _started;
    private bool _sessionMenuOpen;
    private bool _hosting;

    public bool IsConnectionMenuVisible => !_started;
    public bool IsSessionMenuOpen => _sessionMenuOpen;
    public bool IsHosting => _hosting;

    void Start()
    {
        EnsureSessionMenu();

        if (AutoStartPendingSession)
        {
            TryStartPendingSession();
        }
    }

    public void Host()
    {
        if (!Server.StartServer())
        {
            SetStatus("Impossible de démarrer le serveur.");
            return;
        }

        Client.ServerIp = "127.0.0.1";
        if (!Client.Connect())
        {
            SetStatus("Connexion locale échouée.");
            Server.StopServer();
            return;
        }

        _hosting = true;
        HideMenu();
    }

    public void Join()
    {
        Join(null);
    }

    public void Join(string serverIp)
    {
        if (Client == null)
        {
            SetStatus("Client réseau manquant.");
            return;
        }

        Client.ServerIp = ResolveJoinServerIp(serverIp);

        if (!Client.Connect())
        {
            SetStatus("Serveur injoignable : " + Client.ServerIp);
            return;
        }

        HideMenu();
    }

    void Update()
    {
        if (!_started)
        {
            return;
        }

        if (EscapeWasPressed())
        {
            ToggleSessionMenu();
        }

        if (Client != null && !Client.IsConnected)
        {
            ReturnToGameMenu("Connexion perdue.");
        }
    }

    void OnGUI()
    {
        if (!ShowOnGuiMenu) return;

        if (_started)
        {
            DrawSessionMenu();
            return;
        }

        DrawConnectionMenu();
    }

    public void LeaveServer()
    {
        if (Client != null)
        {
            Client.LeaveServer();
        }

        if (_hosting && Server != null)
        {
            Server.StopServer();
        }

        _hosting = false;
        ReturnToGameMenu("Déconnecté.");
    }

    public void ToggleSessionMenu()
    {
        SetSessionMenuOpen(!_sessionMenuOpen);
    }

    public void CloseSessionMenu()
    {
        SetSessionMenuOpen(false);
    }

    public void TryStartPendingSession()
    {
        NetworkSessionRequestData request = NetworkSessionRequest.Consume();
        if (!request.HasRequest)
        {
            return;
        }

        _ip = request.ServerIp;

        if (request.Mode == NetworkSessionMode.Host)
        {
            Host();
            return;
        }

        if (request.Mode == NetworkSessionMode.Join)
        {
            Join(request.ServerIp);
        }
    }

    public string ResolveJoinServerIp(string serverIp)
    {
        if (!string.IsNullOrWhiteSpace(serverIp))
        {
            return serverIp.Trim();
        }

        if (IpInput != null && !string.IsNullOrWhiteSpace(IpInput.text))
        {
            return IpInput.text.Trim();
        }

        if (Client != null && !string.IsNullOrWhiteSpace(Client.ServerIp))
        {
            return Client.ServerIp.Trim();
        }

        return _ip;
    }

    public void EnsureSessionMenu()
    {
        if (SessionMenuPanel != null)
        {
            SessionMenuPanel.SetActive(_sessionMenuOpen);
            return;
        }

        EnsureEventSystem();

        GameObject canvasObject = new GameObject(
            "Network Session Modal Canvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backdrop = CreateImage("Session Backdrop", canvasObject.transform, new Color(0.03f, 0.06f, 0.09f, 0.45f));
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        GameObject shadow = CreateImage("Session Panel Shadow", backdrop.transform, new Color(0f, 0f, 0f, 0.28f));
        RectTransform shadowRect = shadow.GetComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.anchoredPosition = new Vector2(8f, -8f);
        shadowRect.sizeDelta = new Vector2(430f, 220f);

        GameObject panel = CreateImage("Session Panel", backdrop.transform, new Color(0.94f, 0.95f, 0.96f, 0.98f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(430f, 220f);

        GameObject accent = CreateImage("Accent", panel.transform, new Color(0.16f, 0.49f, 0.51f, 1f));
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 6f);

        TMP_Text title = CreateText(
            "Title",
            panel.transform,
            "Session en cours",
            26f,
            FontStyles.Bold,
            new Color(0.09f, 0.13f, 0.18f, 1f),
            TextAlignmentOptions.Left);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -28f);
        titleRect.sizeDelta = new Vector2(-56f, 34f);

        SessionStatusText = CreateText(
            "Status",
            panel.transform,
            "Connecté",
            17f,
            FontStyles.Normal,
            new Color(0.38f, 0.43f, 0.49f, 1f),
            TextAlignmentOptions.Left);
        RectTransform statusRect = SessionStatusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -72f);
        statusRect.sizeDelta = new Vector2(-56f, 28f);

        CreateButton(
            "Continue Button",
            panel.transform,
            "Continuer",
            new Vector2(-94f, 28f),
            new Vector2(168f, 42f),
            new Color(0.88f, 0.90f, 0.92f, 1f),
            new Color(0.09f, 0.13f, 0.18f, 1f),
            CloseSessionMenu);

        CreateButton(
            "Leave Button",
            panel.transform,
            "Quitter le serveur",
            new Vector2(94f, 28f),
            new Vector2(168f, 42f),
            new Color(0.78f, 0.12f, 0.08f, 1f),
            Color.white,
            LeaveServer);

        SessionMenuPanel = backdrop;
        SetSessionMenuOpen(false);
    }

    private void DrawConnectionMenu()
    {
        GUILayout.BeginArea(new Rect(20, 20, 280, 180), GUI.skin.box);
        GUILayout.Label("Réseau");
        GUILayout.Label("IP du serveur :");
        _ip = GUILayout.TextField(_ip);

        if (GUILayout.Button("Héberger (serveur + jouer)"))
            Host();

        if (GUILayout.Button("Rejoindre"))
        {
            Join(_ip);
        }
        GUILayout.EndArea();
    }

    private void DrawSessionMenu()
    {
        if (!_sessionMenuOpen)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(20, 20, 280, 140), GUI.skin.box);
        GUILayout.Label("Session");
        GUILayout.Label(_hosting ? "Hébergement local" : "Connecté à " + Client.ServerIp);

        if (GUILayout.Button("Quitter le serveur"))
        {
            LeaveServer();
        }

        if (GUILayout.Button("Fermer"))
        {
            _sessionMenuOpen = false;
        }

        GUILayout.EndArea();
    }

    private void HideMenu()
    {
        _started = true;
        SetSessionMenuOpen(false);
        if (MenuPanel != null) MenuPanel.SetActive(false);
    }

    private void ShowConnectionMenu()
    {
        _started = false;
        SetSessionMenuOpen(false);
        if (MenuPanel != null) MenuPanel.SetActive(true);
    }

    private void ReturnToGameMenu(string status)
    {
        _started = false;
        SetSessionMenuOpen(false);
        SetStatus(status);

        if (LoadGameMenuOnLeave)
        {
            NetworkSceneLoader.Load(GameMenuSceneName);
            return;
        }

        ShowConnectionMenu();
    }

    private void SetStatus(string message)
    {
        Debug.LogWarning(message);
        if (StatusText != null) StatusText.text = message;
    }

    private void SetSessionMenuOpen(bool isOpen)
    {
        _sessionMenuOpen = isOpen;
        if (SessionStatusText != null)
        {
            string serverIp = Client != null ? Client.ServerIp : _ip;
            SessionStatusText.text = _hosting ? "Hébergement local" : "Connecté à " + serverIp;
        }

        if (SessionMenuPanel != null)
        {
            SessionMenuPanel.SetActive(isOpen);
        }
    }

    private bool EscapeWasPressed()
    {
        bool newInputEscape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool legacyEscape = Input.GetKeyDown(KeyCode.Escape);
        return newInputEscape || legacyEscape;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject(
            "Network Session EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(transform, false);
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);
        imageObject.GetComponent<Image>().color = color;
        return imageObject;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string text,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TMP_Text tmpText = textObject.GetComponent<TMP_Text>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.fontStyle = fontStyle;
        tmpText.color = color;
        tmpText.alignment = alignment;
        tmpText.textWrappingMode = TextWrappingModes.NoWrap;
        return tmpText;
    }

    private static void CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color backgroundColor,
        Color textColor,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateImage(name, parent, backgroundColor);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = sizeDelta;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.14f);
        colors.pressedColor = Color.Lerp(backgroundColor, Color.black, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TMP_Text buttonText = CreateText(
            "Label",
            buttonObject.transform,
            label,
            16f,
            FontStyles.Bold,
            textColor,
            TextAlignmentOptions.Center);
        RectTransform labelRect = buttonText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }
}
