using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Affiche un fil de logs d'événements en bas à gauche de l'écran.
/// Les messages apparaissent avec une animation de fondu et disparaissent après un délai.
/// Usage : GameLogUI.Instance.Log("message")
/// </summary>
public class GameLogUI : MonoBehaviour
{
    public static GameLogUI Instance { get; private set; }

    // ── Configuration ─────────────────────────────────────────────────
    private const int   MaxMessages    = 6;       // lignes max visibles
    private const float MessageLife    = 6f;      // secondes avant disparition
    private const float FadeOutDuration = 1.2f;  // durée du fondu sortant
    private const float FadeInDuration  = 0.25f; // durée du fondu entrant
    private const float PanelWidth     = 380f;
    private const float RowHeight      = 32f;
    private const float Margin         = 16f;

    // ── Couleurs ──────────────────────────────────────────────────────
    private static readonly Color ColBg       = new Color(0.03f, 0.05f, 0.10f, 0.80f);
    private static readonly Color ColJoin     = new Color(0.35f, 1.00f, 0.55f, 1.00f); // vert
    private static readonly Color ColBonus    = new Color(1.00f, 0.82f, 0.10f, 1.00f); // or
    private static readonly Color ColDefault  = new Color(0.80f, 0.88f, 1.00f, 1.00f); // bleu clair
    private static readonly Color ColLeave    = new Color(1.00f, 0.45f, 0.35f, 1.00f); // rouge doux

    // ── Structures internes ───────────────────────────────────────────
    private class LogEntry
    {
        public string  Text;
        public Color   TextColor;
        public float   BirthTime;

        public GameObject GO;
        public TMP_Text   Label;
        public Image      BG;
        public Coroutine  FadeCoroutine;
    }

    private readonly List<LogEntry> _entries = new List<LogEntry>();

    // ── Références UI ─────────────────────────────────────────────────
    private Canvas        _canvas;
    private RectTransform _container;

    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    // ─────────────────────────────────────────────────────────────────
    // API publique
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Affiche un message de connexion d'un joueur.</summary>
    public void LogJoin(string playerName)
        => AddEntry($"🟢  <b>{playerName}</b> a rejoint la partie", ColJoin);

    /// <summary>Affiche un message de déconnexion d'un joueur.</summary>
    public void LogLeave(string playerName)
        => AddEntry($"🔴  <b>{playerName}</b> a quitté la partie", ColLeave);

    /// <summary>Affiche un message de ramassage de bonus.</summary>
    public void LogBonus(string playerName, int score)
        => AddEntry($"⭐  <b>{playerName}</b> a ramassé un bonus  <color=#5BBFEF>({score} total)</color>", ColBonus);

    /// <summary>Message générique.</summary>
    public void Log(string message, Color? color = null)
        => AddEntry(message, color ?? ColDefault);

    // ─────────────────────────────────────────────────────────────────
    // Logique interne
    // ─────────────────────────────────────────────────────────────────

    private void AddEntry(string text, Color textColor)
    {
        // Supprime les anciens si on dépasse la limite
        while (_entries.Count >= MaxMessages)
            RemoveEntry(_entries[0]);

        // Crée la ligne
        LogEntry entry = new LogEntry
        {
            Text      = text,
            TextColor = textColor,
            BirthTime = Time.time,
        };

        BuildRow(entry);
        _entries.Add(entry);

        // Repositionne toutes les lignes (les nouvelles arrivent en bas)
        RepositionRows();

        // Lance le fade-out automatique
        entry.FadeCoroutine = StartCoroutine(LifetimeRoutine(entry));
    }

    private void RemoveEntry(LogEntry entry)
    {
        if (entry.FadeCoroutine != null) StopCoroutine(entry.FadeCoroutine);
        if (entry.GO != null) Destroy(entry.GO);
        _entries.Remove(entry);
    }

    private IEnumerator LifetimeRoutine(LogEntry entry)
    {
        // Fade in
        float t = 0f;
        while (t < FadeInDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / FadeInDuration);
            SetAlpha(entry, alpha);
            yield return null;
        }
        SetAlpha(entry, 1f);

        // Durée de vie
        yield return new WaitForSeconds(MessageLife - FadeInDuration - FadeOutDuration);

        // Fade out
        t = 0f;
        while (t < FadeOutDuration)
        {
            t += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(t / FadeOutDuration);
            SetAlpha(entry, alpha);
            yield return null;
        }

        RemoveEntry(entry);
        RepositionRows();
    }

    private static void SetAlpha(LogEntry entry, float a)
    {
        if (entry.Label != null)
        {
            Color c = entry.Label.color;
            c.a = a;
            entry.Label.color = c;
        }
        if (entry.BG != null)
        {
            Color c = entry.BG.color;
            c.a = ColBg.a * a;
            entry.BG.color = c;
        }
    }

    private void RepositionRows()
    {
        // Les messages les plus récents sont en bas, les plus vieux montent
        for (int i = 0; i < _entries.Count; i++)
        {
            int fromBottom = _entries.Count - 1 - i;
            RectTransform rt = _entries[i].GO.GetComponent<RectTransform>();
            float yPos = fromBottom * RowHeight;
            rt.anchoredPosition = new Vector2(0f, yPos);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Construction UI
    // ─────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Canvas
        GameObject cvGO = new GameObject("GameLogCanvas");
        cvGO.transform.SetParent(transform);
        _canvas = cvGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 99;
        CanvasScaler scaler = cvGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        cvGO.AddComponent<GraphicRaycaster>();

        // Conteneur ancré en bas à gauche
        GameObject containerGO = new GameObject("LogContainer", typeof(RectTransform));
        containerGO.transform.SetParent(cvGO.transform, false);
        _container = containerGO.GetComponent<RectTransform>();
        _container.anchorMin = new Vector2(0f, 0f);
        _container.anchorMax = new Vector2(0f, 0f);
        _container.pivot     = new Vector2(0f, 0f);
        _container.sizeDelta = new Vector2(PanelWidth, RowHeight * MaxMessages);
        _container.anchoredPosition = new Vector2(Margin, Margin);
    }

    private void BuildRow(LogEntry entry)
    {
        // Fond de la ligne
        GameObject rowGO = new GameObject("LogRow", typeof(RectTransform));
        rowGO.transform.SetParent(_container, false);

        RectTransform rt = rowGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(PanelWidth, RowHeight - 2f);

        Image bg = rowGO.AddComponent<Image>();
        bg.color = new Color(ColBg.r, ColBg.g, ColBg.b, 0f); // alpha 0 au départ
        entry.BG = bg;

        // Barre de couleur à gauche
        GameObject barGO = new GameObject("Bar", typeof(RectTransform));
        barGO.transform.SetParent(rowGO.transform, false);
        RectTransform barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = Vector2.zero;
        barRT.anchorMax = new Vector2(0f, 1f);
        barRT.pivot     = new Vector2(0f, 0.5f);
        barRT.sizeDelta = new Vector2(3f, 0f);
        barRT.anchoredPosition = Vector2.zero;
        Image barImg = barGO.AddComponent<Image>();
        barImg.color = entry.TextColor;

        // Texte
        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(rowGO.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 0f);
        textRT.offsetMax = new Vector2(-6f, 0f);

        TMP_Text label = textGO.AddComponent<TextMeshProUGUI>();
        label.text      = entry.Text;
        label.fontSize  = 13;
        label.color     = new Color(entry.TextColor.r, entry.TextColor.g, entry.TextColor.b, 0f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping    = false;
        label.overflowMode          = TextOverflowModes.Ellipsis;
        label.fontStyle             = FontStyles.Normal;
        entry.Label = label;

        entry.GO = rowGO;
    }
}
