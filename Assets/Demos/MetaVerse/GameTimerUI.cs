using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affiche le timer du match en haut au centre de l'écran.
/// Le timer clignote en rouge dans les 30 dernières secondes.
/// </summary>
public class GameTimerUI : MonoBehaviour
{
    public static GameTimerUI Instance { get; private set; }

    private TMP_Text _timerText;
    private Image _panelImage;
    private float _remainingSeconds;
    private bool _initialized;

    // ── Couleurs ──
    private static readonly Color ColPanelBg     = new Color(0.03f, 0.06f, 0.10f, 0.72f);
    private static readonly Color ColTimerNormal  = new Color(0.92f, 0.95f, 1.00f, 1.00f);
    private static readonly Color ColTimerWarning  = new Color(1.00f, 0.30f, 0.20f, 1.00f);
    private static readonly Color ColTimerCritical = new Color(1.00f, 0.12f, 0.08f, 1.00f);

    void Awake()
    {
        Instance = this;
        BuildUI();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!_initialized || _timerText == null) return;

        // Le timer descend localement entre les syncs du serveur
        _remainingSeconds -= Time.deltaTime;
        if (_remainingSeconds < 0f) _remainingSeconds = 0f;

        UpdateDisplay();
    }

    /// <summary>
    /// Met à jour le temps restant (appelé quand le serveur envoie TimerSync).
    /// </summary>
    public void SetRemainingTime(float seconds)
    {
        _remainingSeconds = Mathf.Max(0f, seconds);
        _initialized = true;
        UpdateDisplay();
    }

    /// <summary>Retourne le temps restant actuel.</summary>
    public float RemainingTime => _remainingSeconds;

    private void UpdateDisplay()
    {
        if (_timerText == null) return;

        int totalSeconds = Mathf.CeilToInt(_remainingSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        _timerText.text = $"⏱  {minutes:00}:{seconds:00}";

        // Couleur selon le temps restant
        if (_remainingSeconds <= 10f)
        {
            // Clignotement critique
            float pulse = Mathf.Abs(Mathf.Sin(Time.time * 4f));
            _timerText.color = Color.Lerp(ColTimerCritical, Color.white, pulse * 0.4f);
            _timerText.fontSize = 30f + pulse * 4f;
        }
        else if (_remainingSeconds <= 30f)
        {
            _timerText.color = ColTimerWarning;
            _timerText.fontSize = 30f;
        }
        else
        {
            _timerText.color = ColTimerNormal;
            _timerText.fontSize = 28f;
        }
    }

    private void BuildUI()
    {
        // ── Canvas ──
        GameObject canvasObject = new GameObject("Timer Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ── Panneau ──
        GameObject panel = new GameObject("Timer Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -16f);
        panelRect.sizeDelta = new Vector2(200f, 52f);

        _panelImage = panel.GetComponent<Image>();
        _panelImage.color = ColPanelBg;

        // ── Texte ──
        GameObject textObject = new GameObject("Timer Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        _timerText = textObject.GetComponent<TMP_Text>();
        _timerText.text = "⏱  03:00";
        _timerText.fontSize = 28f;
        _timerText.fontStyle = FontStyles.Bold;
        _timerText.alignment = TextAlignmentOptions.Center;
        _timerText.color = ColTimerNormal;
        _timerText.textWrappingMode = TextWrappingModes.NoWrap;
    }
}
