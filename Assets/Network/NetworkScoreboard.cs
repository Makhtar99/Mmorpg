using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NetworkScoreboard : MonoBehaviour
{
    private class Entry
    {
        public int Id;
        public string PlayerName;
        public int Score;
    }

    public int MaxRows = 10;
    public TMP_Text DisplayText;

    private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

    public int PlayerCount => _entries.Count;

    void Awake()
    {
        EnsureUi();
        Refresh();
    }

    public void UpsertPlayer(int playerId, string playerName, int score)
    {
        _entries[playerId] = new Entry
        {
            Id = playerId,
            PlayerName = PlayerNames.Normalize(playerName),
            Score = Mathf.Max(0, score),
        };
        Refresh();
    }

    public void UpdateScore(int playerId, int score)
    {
        if (_entries.TryGetValue(playerId, out Entry entry))
        {
            entry.Score = Mathf.Max(0, score);
        }
        else
        {
            UpsertPlayer(playerId, "Player " + playerId, score);
            return;
        }

        Refresh();
    }

    public void RemovePlayer(int playerId)
    {
        if (_entries.Remove(playerId))
            Refresh();
    }

    public void Clear()
    {
        _entries.Clear();
        Refresh();
    }

    private void Refresh()
    {
        EnsureUi();
        if (DisplayText == null) return;

        List<Entry> rows = _entries.Values
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.PlayerName)
            .Take(Mathf.Max(1, MaxRows))
            .ToList();

        if (rows.Count == 0)
        {
            DisplayText.text = "Scoreboard\nAucun joueur";
            return;
        }

        var lines = new List<string> { "Scoreboard" };
        for (int i = 0; i < rows.Count; i++)
            lines.Add((i + 1) + ". " + rows[i].PlayerName + " " + rows[i].Score);

        DisplayText.text = string.Join("\n", lines);
    }

    private void EnsureUi()
    {
        if (DisplayText != null) return;

        GameObject canvasObject = new GameObject("Scoreboard Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Scoreboard Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        panelRect.sizeDelta = new Vector2(260f, 320f);

        Image panel = panelObject.GetComponent<Image>();
        panel.color = new Color(0.03f, 0.08f, 0.12f, 0.72f);

        GameObject textObject = new GameObject("Scoreboard Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 14f);
        textRect.offsetMax = new Vector2(-16f, -14f);

        DisplayText = textObject.GetComponent<TMP_Text>();
        DisplayText.fontSize = 22f;
        DisplayText.alignment = TextAlignmentOptions.TopLeft;
        DisplayText.color = Color.white;
        DisplayText.textWrappingMode = TextWrappingModes.NoWrap;
    }
}
