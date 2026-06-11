using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Affiche un classement en haut à droite de l'écran.
/// Utilisé comme singleton, créé automatiquement par GameClient.
/// Appeler LeaderboardUI.Instance.UpdateEntry(playerId, playerName, score)
/// à chaque fois qu'un joueur ramasse un bonus.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    public static LeaderboardUI Instance { get; private set; }

    // ── Configuration ────────────────────────────────────────────────
    private const int    MaxRows      = 8;
    private const float  PanelWidth   = 240f;
    private const float  RowHeight    = 36f;
    private const float  HeaderHeight = 44f;
    private const float  Padding      = 12f;

    // ── Couleurs ─────────────────────────────────────────────────────
    private static readonly Color ColPanel   = new Color(0.04f, 0.05f, 0.12f, 0.88f);
    private static readonly Color ColHeader  = new Color(0.10f, 0.65f, 0.95f, 1.00f);
    private static readonly Color ColGold    = new Color(1.00f, 0.84f, 0.00f, 1.00f);
    private static readonly Color ColSilver  = new Color(0.80f, 0.80f, 0.85f, 1.00f);
    private static readonly Color ColBronze  = new Color(0.85f, 0.52f, 0.20f, 1.00f);
    private static readonly Color ColText    = new Color(0.85f, 0.90f, 1.00f, 1.00f);
    private static readonly Color ColRowEven = new Color(1f, 1f, 1f, 0.04f);
    private static readonly Color ColRowOdd  = new Color(1f, 1f, 1f, 0.00f);
    private static readonly Color ColSelf    = new Color(0.10f, 0.65f, 0.95f, 0.18f);

    // ── Données ──────────────────────────────────────────────────────
    private struct Entry
    {
        public int    PlayerId;
        public string Name;
        public int    Score;
    }

    private readonly List<Entry> _entries = new List<Entry>();

    // ── Références UI ────────────────────────────────────────────────
    private Canvas    _canvas;
    private RectTransform _panel;
    private TMP_Text  _titleText;
    private readonly List<RectTransform> _rows     = new List<RectTransform>();
    private readonly List<TMP_Text>      _rowTexts = new List<TMP_Text>();
    private readonly List<Image>         _rowBGs   = new List<Image>();

    private int _localPlayerId = -1;

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

    public void SetLocalPlayer(int id) => _localPlayerId = id;

    /// <summary>Met à jour ou crée l'entrée d'un joueur, puis rafraîchit l'affichage.</summary>
    public void UpdateEntry(int playerId, string playerName, int score)
    {
        bool found = false;
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].PlayerId == playerId)
            {
                _entries[i] = new Entry { PlayerId = playerId, Name = playerName, Score = score };
                found = true;
                break;
            }
        }
        if (!found)
            _entries.Add(new Entry { PlayerId = playerId, Name = playerName, Score = score });

        // Tri décroissant par score
        _entries.Sort((a, b) => b.Score.CompareTo(a.Score));
        RefreshUI();
    }

    /// <summary>Supprime un joueur du classement (déconnexion).</summary>
    public void RemoveEntry(int playerId)
    {
        _entries.RemoveAll(e => e.PlayerId == playerId);
        RefreshUI();
    }

    // ─────────────────────────────────────────────────────────────────
    // Construction UI (tout par code, aucun prefab nécessaire)
    // ─────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Canvas ──────────────────────────────────────────────────
        GameObject canvasGO = new GameObject("LeaderboardCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel principal ─────────────────────────────────────────
        float totalHeight = HeaderHeight + RowHeight * MaxRows + Padding;
        GameObject panelGO = CreateUIObject("LeaderboardPanel", canvasGO.transform);
        _panel = panelGO.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(1f, 1f);
        _panel.anchorMax = new Vector2(1f, 1f);
        _panel.pivot     = new Vector2(1f, 1f);
        _panel.sizeDelta = new Vector2(PanelWidth, totalHeight);
        _panel.anchoredPosition = new Vector2(-16f, -16f);

        // Fond arrondi du panel
        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = ColPanel;
        SetRoundedCorners(panelImg, 16);

        // Bande titre (dégradé bleu)
        GameObject headerGO = CreateUIObject("Header", _panel);
        RectTransform headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin    = new Vector2(0f, 1f);
        headerRT.anchorMax    = new Vector2(1f, 1f);
        headerRT.pivot        = new Vector2(0.5f, 1f);
        headerRT.sizeDelta    = new Vector2(0f, HeaderHeight);
        headerRT.anchoredPosition = Vector2.zero;
        Image headerImg = headerGO.AddComponent<Image>();
        headerImg.color = ColHeader;
        SetRoundedCorners(headerImg, 12);

        // Icône trophée + titre
        _titleText = CreateText("🏆  Classement", headerGO.transform, 16, FontStyles.Bold);
        RectTransform titleRT = _titleText.GetComponent<RectTransform>();
        StretchFull(titleRT);
        _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.color = Color.white;

        // ── Lignes de classement ────────────────────────────────────
        for (int i = 0; i < MaxRows; i++)
        {
            float yOffset = -(HeaderHeight + i * RowHeight);

            GameObject rowGO = CreateUIObject($"Row_{i}", _panel);
            RectTransform rowRT = rowGO.GetComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0f, 1f);
            rowRT.anchorMax = new Vector2(1f, 1f);
            rowRT.pivot     = new Vector2(0.5f, 1f);
            rowRT.sizeDelta = new Vector2(0f, RowHeight);
            rowRT.anchoredPosition = new Vector2(0f, yOffset);

            Image rowBG = rowGO.AddComponent<Image>();
            rowBG.color = (i % 2 == 0) ? ColRowEven : ColRowOdd;

            TMP_Text rowText = CreateText("", rowGO.transform, 13, FontStyles.Normal);
            RectTransform rtText = rowText.GetComponent<RectTransform>();
            StretchFull(rtText);
            rtText.offsetMin = new Vector2(10f, 0f);
            rtText.offsetMax = new Vector2(-10f, 0f);
            rowText.alignment = TextAlignmentOptions.MidlineLeft;
            rowText.color = ColText;

            rowGO.SetActive(false);

            _rows.Add(rowRT);
            _rowTexts.Add(rowText);
            _rowBGs.Add(rowBG);
        }

        RefreshUI();
    }

    // ─────────────────────────────────────────────────────────────────
    // Rafraîchissement
    // ─────────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        int count = Mathf.Min(_entries.Count, MaxRows);

        // Redimensionne le panel selon le nombre de lignes visibles
        float newH = HeaderHeight + RowHeight * count + Padding;
        _panel.sizeDelta = new Vector2(PanelWidth, newH);

        for (int i = 0; i < MaxRows; i++)
        {
            if (i >= count)
            {
                _rows[i].gameObject.SetActive(false);
                continue;
            }

            _rows[i].gameObject.SetActive(true);
            Entry e = _entries[i];
            bool isLocal = e.PlayerId == _localPlayerId;

            // Couleur de fond
            _rowBGs[i].color = isLocal ? ColSelf : ((i % 2 == 0) ? ColRowEven : ColRowOdd);

            // Médaille ou numéro
            string rank = i switch
            {
                0 => "<color=#FFD700>🥇</color>",
                1 => "<color=#C0C0C0>🥈</color>",
                2 => "<color=#CD7F32>🥉</color>",
                _ => $"<color=#8899BB>#{i + 1}</color>"
            };

            string you = isLocal ? " <color=#5BBFEF><b>(vous)</b></color>" : "";
            string bonus = e.Score == 1 ? "bonus" : "bonus";

            _rowTexts[i].text = $"{rank}  <b>{e.Name}</b>{you}  —  <color=#5BBFEF>{e.Score} {bonus}</color>";
        }

        // Masque le panel entier si personne n'a encore de score
        _panel.gameObject.SetActive(count > 0);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers UI
    // ─────────────────────────────────────────────────────────────────

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TMP_Text CreateText(string content, Transform parent, int size, FontStyles style)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text       = content;
        t.fontSize   = size;
        t.fontStyle  = style;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetRoundedCorners(Image img, float radius)
    {
        // Simule les coins arrondis avec le sprite UI default de Unity
        img.sprite = Resources.Load<Sprite>("UI/Skin/UISprite");
        img.type   = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
    }
}
