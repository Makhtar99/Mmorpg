using UnityEngine;

public class GameMenu : MonoBehaviour
{
    public string GameSceneName = "MetaVerse";
    public string ServerIp = "127.0.0.1";
    public bool ShowOnGuiMenu = true;

    public TMPro.TMP_InputField IpInput;
    public TMPro.TMP_Text StatusText;

    void Start()
    {
        if (IpInput != null)
        {
            IpInput.text = ServerIp;
        }
    }

    void OnGUI()
    {
        if (!ShowOnGuiMenu)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(20, 20, 320, 170), GUI.skin.box);
        GUILayout.Label("MMPORG Metaverse");
        GUILayout.Label("IP du serveur :");
        ServerIp = GUILayout.TextField(ServerIp);

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
        NetworkSessionRequest.Host();
        NetworkSceneLoader.Load(GameSceneName);
    }

    public void Join()
    {
        if (IpInput != null && !string.IsNullOrWhiteSpace(IpInput.text))
        {
            ServerIp = IpInput.text.Trim();
        }

        NetworkSessionRequest.Join(ServerIp);
        NetworkSceneLoader.Load(GameSceneName);
    }

    public void SetStatus(string message)
    {
        if (StatusText != null)
        {
            StatusText.text = message;
        }
    }
}
