using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Écran de fin de partie affichant le top 3 des joueurs.
/// Créé dynamiquement par GameClient lorsque le serveur envoie GameOver.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    // ── Données ──────────────────────────────────────────────────────
    public struct PlayerResult
    {
        public int Id;
        public string Name;
        public int Score;
    }

    // ── Couleurs ─────────────────────────────────────────────────────
    private static readonly Color ColOverlay    = new Color(0.02f, 0.03f, 0.06f, 0.88f);
    private static readonly Color ColPanel      = new Color(0.08f, 0.10f, 0.16f, 0.95f);
    private static readonly Color ColGold       = new Color(1.00f, 0.84f, 0.00f, 1.00f);
    private static readonly Color ColSilver     = new Color(0.78f, 0.82f, 0.88f, 1.00f);
    private static readonly Color ColBronze     = new Color(0.80f, 0.50f, 0.20f, 1.00f);
    private static readonly Color ColTitle      = new Color(1.00f, 1.00f, 1.00f, 1.00f);
    private static readonly Color ColSubtitle   = new Color(0.60f, 0.65f, 0.75f, 1.00f);
    private static readonly Color ColScore      = new Color(0.45f, 0.82f, 1.00f, 1.00f);
    private static readonly Color ColNameText   = new Color(0.92f, 0.94f, 0.96f, 1.00f);

    private static readonly string[] RankEmojis   = { "🏆", "🥈", "🥉" };
    private static readonly string[] RankLabels   = { "1er", "2ème", "3ème" };
    private static readonly Color[]  RankColors   = { ColGold, ColSilver, ColBronze };

    // ── Références UI ────────────────────────────────────────────────
    private CanvasGroup _canvasGroup;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Affiche l'écran de fin de partie avec les résultats.
    /// </summary>
    public void Show(List<PlayerResult> topPlayers)
    {
        BuildUI(topPlayers);
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        if (_canvasGroup == null) yield break;

        _canvasGroup.alpha = 0f;
        float duration = 0.8f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t / duration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;
    }

    private void BuildUI(List<PlayerResult> topPlayers)
    {
        // ── Canvas ──
        GameObject canvasObject = new GameObject("GameOver Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        // ── Overlay sombre ──
        GameObject overlay = CreateImage("Overlay", canvasObject.transform, ColOverlay);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        // ── Panneau central ──
        GameObject panel = CreateImage("Panel", overlay.transform, ColPanel);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(560f, 480f);

        // ── Barre d'accent dorée en haut ──
        GameObject accent = CreateImage("Gold Accent", panel.transform, ColGold);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 5f);

        // ── Titre "Fin de partie" ──
        TMP_Text title = CreateText("Title", panel.transform, "⏱  FIN DE PARTIE", 36f, FontStyles.Bold, ColTitle, TextAlignmentOptions.Center);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        titleRect.sizeDelta = new Vector2(-40f, 48f);

        // ── Sous-titre ──
        TMP_Text subtitle = CreateText("Subtitle", panel.transform, "Top 3 — Bonus Ramassés", 18f, FontStyles.Normal, ColSubtitle, TextAlignmentOptions.Center);
        RectTransform subtitleRect = subtitle.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.anchoredPosition = new Vector2(0f, -90f);
        subtitleRect.sizeDelta = new Vector2(-40f, 30f);

        // ── Séparateur ──
        GameObject separator = CreateImage("Separator", panel.transform, new Color(1f, 1f, 1f, 0.12f));
        RectTransform sepRect = separator.GetComponent<RectTransform>();
        sepRect.anchorMin = new Vector2(0.1f, 1f);
        sepRect.anchorMax = new Vector2(0.9f, 1f);
        sepRect.pivot = new Vector2(0.5f, 0.5f);
        sepRect.anchoredPosition = new Vector2(0f, -126f);
        sepRect.sizeDelta = new Vector2(0f, 2f);

        // ── Lignes de joueurs ──
        float startY = -156f;
        float rowHeight = 90f;

        for (int i = 0; i < topPlayers.Count && i < 3; i++)
        {
            BuildPlayerRow(panel.transform, topPlayers[i], i, startY - i * rowHeight);
        }

        // ── Message si aucun joueur ──
        if (topPlayers.Count == 0)
        {
            TMP_Text noPlayer = CreateText("NoPlayer", panel.transform, "Aucun joueur n'a ramassé de bonus.", 20f, FontStyles.Italic, ColSubtitle, TextAlignmentOptions.Center);
            RectTransform noPlayerRect = noPlayer.GetComponent<RectTransform>();
            noPlayerRect.anchorMin = new Vector2(0f, 0.5f);
            noPlayerRect.anchorMax = new Vector2(1f, 0.5f);
            noPlayerRect.pivot = new Vector2(0.5f, 0.5f);
            noPlayerRect.anchoredPosition = Vector2.zero;
            noPlayerRect.sizeDelta = new Vector2(-40f, 40f);
        }

        // ── Texte en bas "Appuyez sur Échap" ──
        TMP_Text hint = CreateText("Hint", panel.transform, "Appuyez sur Échap pour continuer", 14f, FontStyles.Italic, new Color(0.5f, 0.55f, 0.65f, 0.8f), TextAlignmentOptions.Center);
        RectTransform hintRect = hint.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 16f);
        hintRect.sizeDelta = new Vector2(-40f, 28f);
    }

    private void BuildPlayerRow(Transform parent, PlayerResult player, int rank, float yPosition)
    {
        Color rankColor = RankColors[Mathf.Min(rank, RankColors.Length - 1)];

        // ── Fond de la ligne ──
        Color rowBgColor = new Color(rankColor.r, rankColor.g, rankColor.b, 0.08f + (2 - rank) * 0.04f);
        GameObject row = CreateImage("Player Row " + rank, parent, rowBgColor);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.05f, 1f);
        rowRect.anchorMax = new Vector2(0.95f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, yPosition);
        rowRect.sizeDelta = new Vector2(0f, 78f);

        // ── Barre de couleur à gauche ──
        GameObject bar = CreateImage("Rank Bar", row.transform, rankColor);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(5f, 0f);

        // ── Emoji de rang ──
        string emoji = rank < RankEmojis.Length ? RankEmojis[rank] : "•";
        TMP_Text emojiText = CreateText("Emoji", row.transform, emoji, 32f, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);
        RectTransform emojiRect = emojiText.GetComponent<RectTransform>();
        emojiRect.anchorMin = new Vector2(0f, 0f);
        emojiRect.anchorMax = new Vector2(0f, 1f);
        emojiRect.pivot = new Vector2(0f, 0.5f);
        emojiRect.anchoredPosition = new Vector2(18f, 0f);
        emojiRect.sizeDelta = new Vector2(50f, 0f);

        // ── Rang texte ──
        string rankLabel = rank < RankLabels.Length ? RankLabels[rank] : (rank + 1) + "ème";
        TMP_Text rankText = CreateText("Rank", row.transform, rankLabel, 14f, FontStyles.Bold, rankColor, TextAlignmentOptions.Left);
        RectTransform rankRect = rankText.GetComponent<RectTransform>();
        rankRect.anchorMin = new Vector2(0f, 1f);
        rankRect.anchorMax = new Vector2(0f, 1f);
        rankRect.pivot = new Vector2(0f, 1f);
        rankRect.anchoredPosition = new Vector2(72f, -12f);
        rankRect.sizeDelta = new Vector2(60f, 20f);

        // ── Nom du joueur ──
        TMP_Text nameText = CreateText("Name", row.transform, player.Name, 24f, FontStyles.Bold, ColNameText, TextAlignmentOptions.Left);
        RectTransform nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(0.7f, 1f);
        nameRect.pivot = new Vector2(0f, 0.5f);
        nameRect.anchoredPosition = new Vector2(72f, -6f);
        nameRect.sizeDelta = new Vector2(0f, 0f);

        // ── Score ──
        TMP_Text scoreText = CreateText("Score", row.transform, player.Score + " pts", 26f, FontStyles.Bold, ColScore, TextAlignmentOptions.Right);
        RectTransform scoreRect = scoreText.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0.65f, 0f);
        scoreRect.anchorMax = new Vector2(1f, 1f);
        scoreRect.pivot = new Vector2(1f, 0.5f);
        scoreRect.anchoredPosition = new Vector2(-16f, 0f);
        scoreRect.sizeDelta = new Vector2(0f, 0f);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
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
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TMP_Text tmp = go.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }
}
