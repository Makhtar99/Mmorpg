using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affiche le multiplicateur de vitesse du joueur local en bas à droite de l'écran.
/// La vitesse de base est 1.00 et augmente à chaque bonus ramassé.
/// </summary>
public class PlayerSpeedUI : MonoBehaviour
{
    public static PlayerSpeedUI Instance { get; private set; }

    private TMP_Text _speedText;
    private float _speedMultiplier = 1f;

    // ── Couleurs ──
    private static readonly Color ColPanelBg     = new Color(0.03f, 0.06f, 0.10f, 0.72f);
    private static readonly Color ColSpeedBase   = new Color(0.55f, 0.62f, 0.72f, 1.00f);
    private static readonly Color ColSpeedMid    = new Color(0.40f, 0.85f, 1.00f, 1.00f);
    private static readonly Color ColSpeedFast   = new Color(0.30f, 1.00f, 0.55f, 1.00f);
    private static readonly Color ColSpeedMax    = new Color(1.00f, 0.84f, 0.00f, 1.00f);
    private static readonly Color ColLabel       = new Color(0.55f, 0.60f, 0.70f, 0.85f);

    void Awake()
    {
        Instance = this;
        BuildUI();
        UpdateDisplay();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Retourne le multiplicateur de vitesse actuel.</summary>
    public float SpeedMultiplier => _speedMultiplier;

    /// <summary>
    /// Définit le multiplicateur de vitesse et met à jour l'affichage.
    /// Appelé par GameClient tous les 5 bonus ramassés.
    /// </summary>
    public void SetMultiplier(float multiplier)
    {
        _speedMultiplier = Mathf.Clamp(multiplier, 1f, 2f);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_speedText == null) return;

        bool isMax = _speedMultiplier >= 1.99f;
        _speedText.text = isMax
            ? $"🏃  x{_speedMultiplier:F2} MAX"
            : $"🏃  x{_speedMultiplier:F2}";

        // Couleur progressive : base → cyan → vert → or (MAX)
        float t = Mathf.Clamp01((_speedMultiplier - 1f) / 1f); // 0 à 1 entre x1 et x2
        if (t < 0.01f)
            _speedText.color = ColSpeedBase;
        else if (isMax)
            _speedText.color = ColSpeedMax;
        else
            _speedText.color = Color.Lerp(ColSpeedMid, ColSpeedFast, t);
    }

    private void BuildUI()
    {
        // ── Canvas ──
        GameObject canvasObject = new GameObject("Speed Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ── Panneau en bas à droite ──
        GameObject panel = new GameObject("Speed Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-16f, 16f);
        panelRect.sizeDelta = new Vector2(180f, 42f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = ColPanelBg;

        // ── Label "Vitesse" ──
        GameObject labelObject = new GameObject("Speed Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panel.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, 18f);
        labelRect.sizeDelta = new Vector2(-16f, 18f);

        TMP_Text labelText = labelObject.GetComponent<TMP_Text>();
        labelText.text = "Vitesse";
        labelText.fontSize = 12f;
        labelText.fontStyle = FontStyles.Normal;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = ColLabel;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;

        // ── Texte vitesse ──
        GameObject textObject = new GameObject("Speed Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        _speedText = textObject.GetComponent<TMP_Text>();
        _speedText.text = "🏃  x1.00";
        _speedText.fontSize = 20f;
        _speedText.fontStyle = FontStyles.Bold;
        _speedText.alignment = TextAlignmentOptions.Center;
        _speedText.color = ColSpeedBase;
        _speedText.textWrappingMode = TextWrappingModes.NoWrap;
    }
}
