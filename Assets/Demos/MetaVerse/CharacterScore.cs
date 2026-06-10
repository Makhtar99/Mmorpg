using UnityEngine;
using TMPro;

public class CharacterScore : MonoBehaviour
{
    private const float NameOffsetBelowScore = 0.45f;

    public int Score = 0;
    public string PlayerName = PlayerNames.DefaultName;
    public TMP_Text TxtScore;
    public TMP_Text TxtName;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (TxtScore != null) TxtScore.text = Score.ToString();
        SetPlayerName(PlayerName);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void AddScore(int points) {
      Score += points;
      if (TxtScore != null) TxtScore.text = Score.ToString();
    }

    public void SetScore(int value) {
      Score = value;
      if (TxtScore != null) TxtScore.text = Score.ToString();
    }

    public void SetPlayerName(string playerName)
    {
      PlayerName = PlayerNames.Normalize(playerName);
      EnsureNameText();
      TxtName.text = PlayerName;
    }

    private void EnsureNameText()
    {
      if (TxtName != null) return;

      GameObject nameObject = new GameObject(
        "Player Name (TMP)",
        typeof(RectTransform),
        typeof(MeshRenderer),
        typeof(TextMeshPro));
      nameObject.transform.SetParent(transform, false);

      RectTransform rect = nameObject.GetComponent<RectTransform>();
      PositionNameText(rect);

      TxtName = nameObject.GetComponent<TMP_Text>();
      TxtName.fontSize = 26f;
      TxtName.alignment = TextAlignmentOptions.Center;
      TxtName.color = Color.white;
      TxtName.textWrappingMode = TextWrappingModes.NoWrap;

      if (TxtScore != null)
      {
        TxtName.font = TxtScore.font;
        TxtName.fontSharedMaterial = TxtScore.fontSharedMaterial;
      }
    }

    private void PositionNameText(RectTransform nameRect)
    {
      if (TxtScore == null)
      {
        nameRect.localPosition = new Vector3(0f, 2.55f, 0f);
        nameRect.localRotation = Quaternion.identity;
        nameRect.localScale = Vector3.one * 0.1f;
        nameRect.sizeDelta = new Vector2(24f, 4f);
        return;
      }

      RectTransform scoreRect = TxtScore.GetComponent<RectTransform>();
      if (scoreRect == null)
      {
        nameRect.localPosition = TxtScore.transform.localPosition + new Vector3(0f, -NameOffsetBelowScore, 0f);
        nameRect.localRotation = TxtScore.transform.localRotation;
        nameRect.localScale = TxtScore.transform.localScale;
        nameRect.sizeDelta = new Vector2(24f, 4f);
        return;
      }

      nameRect.localPosition = scoreRect.localPosition + new Vector3(0f, -NameOffsetBelowScore, 0f);
      nameRect.localRotation = scoreRect.localRotation;
      nameRect.localScale = scoreRect.localScale;
      nameRect.sizeDelta = new Vector2(Mathf.Max(scoreRect.sizeDelta.x, 24f), Mathf.Max(scoreRect.sizeDelta.y * 0.8f, 4f));
    }
}
