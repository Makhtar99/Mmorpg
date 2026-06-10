using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GameMenu : MonoBehaviour
{
    public string GameSceneName = "MetaVerse";
    public string ServerIp = "127.0.0.1";
    public string PlayerName = PlayerNames.DefaultName;
    public bool ShowOnGuiMenu = true;
    public byte SelectedCharacter = 0;
    public string[] CharacterNames = { "Barbare", "Ingenieur", "Druide", "Chevalier", "Mage", "Rogue" };
    public GameObject[] CharacterPreviewPrefabs;

    public TMP_InputField IpInput;
    public TMP_InputField PlayerNameInput;
    public TMP_Text StatusText;
    public GameObject CharacterSelectionPanel;
    public Transform CharacterPreviewRoot;
    public TMP_Text CharacterNameText;
    public Button PreviousCharacterButton;
    public Button NextCharacterButton;

    private Camera _characterPreviewCamera;
    private RenderTexture _characterPreviewTexture;
    private GameObject _currentPreview;
    private int _previewedCharacter = -1;
    private bool _ownsCharacterPreviewRoot;

    void Start()
    {
        if (IpInput != null)
        {
            IpInput.text = ServerIp;
        }

        if (PlayerNameInput != null)
        {
            PlayerNameInput.text = PlayerName;
        }

        EnsureMenuBackground();
        EnsureCharacterSelectionUi();
        UpdateCharacterSelectionUi();
    }

    void OnGUI()
    {
        if (!ShowOnGuiMenu)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(20, 20, 360, 260), GUI.skin.box);
        GUILayout.Label("MMPORG Metaverse");
        GUILayout.Label("Nom du joueur :");
        PlayerName = GUILayout.TextField(PlayerName);
        GUILayout.Label("IP du serveur :");
        ServerIp = GUILayout.TextField(ServerIp);
        GUILayout.Space(8f);
        GUILayout.Label("Personnage : " + SelectedCharacterName);
        DrawCharacterSelectionOnGui();
        GUILayout.Space(8f);

        if (GUILayout.Button("Héberger (serveur + jouer)"))
        {
            Host();
        }

        if (GUILayout.Button("Rejoindre"))
        {
            Join();
        }

        GUILayout.EndArea();
    }

    public void Host()
    {
        RefreshMenuValues();
        NetworkSessionRequest.Host(SelectedCharacter, PlayerName);
        NetworkSceneLoader.Load(GameSceneName);
    }

    public void Join()
    {
        RefreshMenuValues();
        NetworkSessionRequest.Join(ServerIp, SelectedCharacter, PlayerName);
        NetworkSceneLoader.Load(GameSceneName);
    }

    public void SelectCharacter(int index)
    {
        SelectedCharacter = (byte)Mathf.Clamp(index, 0, CharacterCount - 1);
        UpdateCharacterSelectionUi();
    }

    public void NextCharacter()
    {
        SelectCharacter((SelectedCharacter + 1) % CharacterCount);
    }

    public void PreviousCharacter()
    {
        SelectCharacter((SelectedCharacter + CharacterCount - 1) % CharacterCount);
    }

    public void EnsureCharacterSelectionUi()
    {
        SelectCharacter(SelectedCharacter);

        if (ShowOnGuiMenu)
        {
            return;
        }

        Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            return;
        }

        StyleCanvasBackground(canvas.transform);

        if (CharacterSelectionPanel != null)
        {
            UpdateCharacterSelectionUi();
            return;
        }

        Transform panel = FindChildByName(canvas.transform, "Connection Panel") ?? canvas.transform;
        ExpandConnectionPanel(panel);
        StylizeConnectionPanel(panel);
        RepositionExistingControls(panel);
        EnsurePreviewCamera();
        EnsurePlayerNameInput(panel);

        CharacterSelectionPanel = new GameObject("Character Selection", typeof(RectTransform));
        CharacterSelectionPanel.transform.SetParent(panel, false);

        RectTransform selectionRect = CharacterSelectionPanel.GetComponent<RectTransform>();
        selectionRect.anchorMin = Vector2.zero;
        selectionRect.anchorMax = Vector2.one;
        selectionRect.offsetMin = Vector2.zero;
        selectionRect.offsetMax = Vector2.zero;

        TMP_Text label = CreateText(
            "Character Label",
            CharacterSelectionPanel.transform,
            "Personnage",
            16f,
            FontStyles.Bold,
            new Color(0.09f, 0.13f, 0.18f, 1f),
            TextAlignmentOptions.Center);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, -342f);
        labelRect.sizeDelta = new Vector2(-72f, 28f);
        CharacterNameText = label;

        CreatePreviewViewport(CharacterSelectionPanel.transform);
        PreviousCharacterButton = CreateButton(
            "Previous Character Button",
            CharacterSelectionPanel.transform,
            "<",
            new Vector2(-230f, -472f),
            new Vector2(54f, 76f),
            new Color(0.88f, 0.90f, 0.92f, 1f),
            new Color(0.09f, 0.13f, 0.18f, 1f),
            PreviousCharacter);
        NextCharacterButton = CreateButton(
            "Next Character Button",
            CharacterSelectionPanel.transform,
            ">",
            new Vector2(230f, -472f),
            new Vector2(54f, 76f),
            new Color(0.88f, 0.90f, 0.92f, 1f),
            new Color(0.09f, 0.13f, 0.18f, 1f),
            NextCharacter);

        UpdateCharacterSelectionUi();
    }

    void OnDestroy()
    {
        ClearCurrentPreview();

        if (_characterPreviewCamera != null)
        {
            DestroyImmediateSafe(_characterPreviewCamera.gameObject);
            _characterPreviewCamera = null;
        }

        if (_characterPreviewTexture != null)
        {
            _characterPreviewTexture.Release();
            DestroyImmediateSafe(_characterPreviewTexture);
            _characterPreviewTexture = null;
        }

        if (_ownsCharacterPreviewRoot && CharacterPreviewRoot != null)
        {
            DestroyImmediateSafe(CharacterPreviewRoot.gameObject);
            CharacterPreviewRoot = null;
        }

    }

    public void SetStatus(string message)
    {
        if (StatusText != null)
        {
            StatusText.text = message;
        }
    }

    private void RefreshMenuValues()
    {
        if (IpInput != null && !string.IsNullOrWhiteSpace(IpInput.text))
        {
            ServerIp = IpInput.text.Trim();
        }

        if (PlayerNameInput != null)
        {
            PlayerName = PlayerNameInput.text;
        }

        PlayerName = PlayerNames.Normalize(PlayerName);
        if (PlayerNameInput != null)
        {
            PlayerNameInput.text = PlayerName;
        }
    }

    private string SelectedCharacterName => CharacterNameAt(SelectedCharacter);

    private int CharacterCount => CharacterNames == null || CharacterNames.Length == 0 ? 1 : CharacterNames.Length;

    private string CharacterNameAt(int index)
    {
        if (CharacterNames == null || CharacterNames.Length == 0)
        {
            return "Personnage 1";
        }

        int safeIndex = Mathf.Clamp(index, 0, CharacterNames.Length - 1);
        return string.IsNullOrWhiteSpace(CharacterNames[safeIndex])
            ? "Personnage " + (safeIndex + 1)
            : CharacterNames[safeIndex];
    }

    private void DrawCharacterSelectionOnGui()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(48f)))
        {
            PreviousCharacter();
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label(SelectedCharacterName);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(">", GUILayout.Width(48f)))
        {
            NextCharacter();
        }

        GUILayout.EndHorizontal();
    }

    private void UpdateCharacterSelectionUi()
    {
        if (CharacterNameText != null)
        {
            CharacterNameText.text = SelectedCharacterName;
        }

        RefreshCharacterPreview();
    }

    private static void ExpandConnectionPanel(Transform panel)
    {
        RectTransform rect = panel as RectTransform;
        if (rect == null)
        {
            return;
        }

        rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 620f), Mathf.Max(rect.sizeDelta.y, 760f));
    }

    private static void StylizeConnectionPanel(Transform panel)
    {
        Image image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.94f, 0.95f, 0.96f, 0.88f);
        }
    }

    private static void RepositionExistingControls(Transform panel)
    {
        SetChildAnchoredPosition(panel, "IP Label", new Vector2(0f, -238f));
        SetChildAnchoredPosition(panel, "Server IP Input", new Vector2(0f, -282f));
        SetChildAnchoredPosition(panel, "Héberger Button", new Vector2(0f, -630f));
        SetChildAnchoredPosition(panel, "Rejoindre Button", new Vector2(0f, -690f));
    }

    private static void StyleCanvasBackground(Transform canvas)
    {
        Transform namedBackground = FindChildByName(canvas, "Background");
        Image namedBackgroundImage = namedBackground != null ? namedBackground.GetComponent<Image>() : null;
        if (namedBackgroundImage != null)
        {
            StyleBackgroundImage(namedBackgroundImage);
            return;
        }

        Image[] images = canvas.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image.transform.name == "Connection Panel")
            {
                continue;
            }

            RectTransform rect = image.transform as RectTransform;
            if (rect != null && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one)
            {
                StyleBackgroundImage(image);
            }
        }
    }

    private static void StyleBackgroundImage(Image image)
    {
        image.color = new Color(0.10f, 0.11f, 0.10f, 1f);
        image.raycastTarget = false;
    }

    private static void SetChildAnchoredPosition(Transform parent, string childName, Vector2 position)
    {
        Transform child = FindChildByName(parent, childName);
        RectTransform rect = child as RectTransform;
        if (rect != null)
        {
            rect.anchoredPosition = position;
        }
    }

    private static Transform FindChildByName(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = FindChildByName(parent.GetChild(i), childName);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private void CreatePreviewViewport(Transform parent)
    {
        GameObject previewObject = new GameObject(
            "Character Preview Viewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(Button));
        previewObject.transform.SetParent(parent, false);

        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0.5f, 1f);
        previewRect.anchorMax = new Vector2(0.5f, 1f);
        previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.anchoredPosition = new Vector2(0f, -472f);
        previewRect.sizeDelta = new Vector2(380f, 260f);

        RawImage previewImage = previewObject.GetComponent<RawImage>();
        previewImage.texture = _characterPreviewTexture;
        previewImage.color = Color.white;

        Button previewButton = previewObject.GetComponent<Button>();
        previewButton.targetGraphic = previewImage;
        previewButton.onClick.AddListener(NextCharacter);
    }

    private void EnsurePreviewCamera()
    {
        if (CharacterPreviewRoot == null)
        {
            GameObject root = new GameObject("Character Preview Root");
            root.transform.position = new Vector3(5000f, 0f, 0f);
            CharacterPreviewRoot = root.transform;
            _ownsCharacterPreviewRoot = true;
        }

        if (_characterPreviewTexture == null)
        {
            _characterPreviewTexture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
            _characterPreviewTexture.name = "Character Preview Texture";
        }

        if (_characterPreviewCamera != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("Character Preview Camera", typeof(Camera));
        _characterPreviewCamera = cameraObject.GetComponent<Camera>();
        _characterPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        _characterPreviewCamera.backgroundColor = new Color(0.94f, 0.95f, 0.96f, 0f);
        _characterPreviewCamera.orthographic = true;
        _characterPreviewCamera.orthographicSize = 0.95f;
        _characterPreviewCamera.nearClipPlane = 0.1f;
        _characterPreviewCamera.farClipPlane = 20f;
        _characterPreviewCamera.targetTexture = _characterPreviewTexture;
        _characterPreviewCamera.transform.position = CharacterPreviewRoot.position + new Vector3(0f, 0.9f, -3.7f);
        _characterPreviewCamera.transform.LookAt(CharacterPreviewRoot.position + new Vector3(0f, 0.85f, 0f));
    }

    private void RefreshCharacterPreview()
    {
        if (CharacterPreviewRoot == null)
        {
            return;
        }

        if (_previewedCharacter == SelectedCharacter && _currentPreview != null)
        {
            return;
        }

        ClearCurrentPreview();
        _previewedCharacter = SelectedCharacter;

        GameObject prefab = CharacterPreviewPrefabAt(SelectedCharacter);
        if (prefab == null)
        {
            return;
        }

        _currentPreview = Instantiate(prefab, CharacterPreviewRoot);
        _currentPreview.name = prefab.name + " Preview";
        PreparePreviewInstance(_currentPreview);
    }

    private GameObject CharacterPreviewPrefabAt(int index)
    {
        if (CharacterPreviewPrefabs == null || CharacterPreviewPrefabs.Length == 0)
        {
            return null;
        }

        int safeIndex = Mathf.Clamp(index, 0, CharacterPreviewPrefabs.Length - 1);
        return CharacterPreviewPrefabs[safeIndex];
    }

    private void ClearCurrentPreview()
    {
        if (_currentPreview != null)
        {
            DestroyImmediateSafe(_currentPreview);
            _currentPreview = null;
        }

        if (CharacterPreviewRoot == null)
        {
            return;
        }

        for (int i = CharacterPreviewRoot.childCount - 1; i >= 0; i--)
        {
            DestroyImmediateSafe(CharacterPreviewRoot.GetChild(i).gameObject);
        }
    }

    private void PreparePreviewInstance(GameObject instance)
    {
        foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
        }

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody rigidbody in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
        }

        instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        CenterPreviewInstance(instance);
    }

    private void CenterPreviewInstance(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            instance.transform.localPosition = Vector3.zero;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float maxSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxSize > 0.001f)
        {
            float scale = 2.45f / maxSize;
            instance.transform.localScale *= scale;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 targetCenter = CharacterPreviewRoot.position + new Vector3(0f, 0.75f, 0f);
        instance.transform.position += targetCenter - bounds.center;
    }

    private void EnsurePlayerNameInput(Transform panel)
    {
        if (PlayerNameInput != null)
        {
            PlayerNameInput.text = PlayerName;
            return;
        }

        TMP_Text label = CreateText(
            "Player Name Label",
            panel,
            "Nom du joueur",
            18f,
            FontStyles.Bold,
            new Color(0.09f, 0.13f, 0.18f, 1f),
            TextAlignmentOptions.Left);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, -145f);
        labelRect.sizeDelta = new Vector2(-72f, 28f);

        PlayerNameInput = CreateInputField(
            "Player Name Input",
            panel,
            PlayerName,
            "Votre nom",
            new Vector2(0f, -188f),
            new Vector2(-72f, 52f));
    }

    private void EnsureMenuBackground()
    {
        Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.11f, 0.10f, 1f);
        }
    }

    private static void DestroyImmediateSafe(Object objectToDestroy)
    {
        if (Application.isPlaying)
        {
            Destroy(objectToDestroy);
            return;
        }

        DestroyImmediate(objectToDestroy);
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

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color backgroundColor,
        Color textColor,
        UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 1f);
        buttonRect.anchorMax = new Vector2(0.5f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = sizeDelta;

        Image image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        TMP_Text buttonText = CreateText(
            "Label",
            buttonObject.transform,
            label,
            14f,
            FontStyles.Bold,
            textColor,
            TextAlignmentOptions.Center);
        RectTransform labelRect = buttonText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private static TMP_InputField CreateInputField(
        string name,
        Transform parent,
        string value,
        string placeholder,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject inputObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(TMP_InputField));
        inputObject.transform.SetParent(parent, false);

        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 1f);
        inputRect.anchorMax = new Vector2(1f, 1f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = anchoredPosition;
        inputRect.sizeDelta = sizeDelta;

        Image background = inputObject.GetComponent<Image>();
        background.color = new Color(0.88f, 0.91f, 0.93f, 0.92f);

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(inputObject.transform, false);
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(18f, 8f);
        textAreaRect.offsetMax = new Vector2(-18f, -8f);

        TMP_Text placeholderText = CreateText(
            "Placeholder",
            textArea.transform,
            placeholder,
            18f,
            FontStyles.Italic,
            new Color(0.46f, 0.50f, 0.55f, 1f),
            TextAlignmentOptions.Left);
        RectTransform placeholderRect = placeholderText.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;

        TMP_Text text = CreateText(
            "Text",
            textArea.transform,
            value,
            18f,
            FontStyles.Normal,
            new Color(0.09f, 0.13f, 0.18f, 1f),
            TextAlignmentOptions.Left);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textViewport = textAreaRect;
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.text = value;
        input.characterLimit = PlayerNames.MaxLength;
        return input;
    }
}
