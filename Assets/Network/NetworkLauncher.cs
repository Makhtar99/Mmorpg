using UnityEngine;

public class NetworkLauncher : MonoBehaviour
{
    public GameServer Server;
    public GameClient Client;

    public bool ShowOnGuiMenu = true;

    public TMPro.TMP_InputField IpInput;
    public TMPro.TMP_Text StatusText;
    public GameObject MenuPanel;

    private string _ip = "127.0.0.1";
    private bool _started;

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
            return;
        }

        HideMenu();
    }

    public void Join()
    {
        if (IpInput != null && !string.IsNullOrWhiteSpace(IpInput.text))
            Client.ServerIp = IpInput.text.Trim();

        if (!Client.Connect())
        {
            SetStatus("Serveur injoignable : " + Client.ServerIp);
            return;
        }

        HideMenu();
    }

    void OnGUI()
    {
        if (!ShowOnGuiMenu || _started) return;

        GUILayout.BeginArea(new Rect(20, 20, 280, 180), GUI.skin.box);
        GUILayout.Label("Réseau");
        GUILayout.Label("IP du serveur :");
        _ip = GUILayout.TextField(_ip);

        if (GUILayout.Button("Héberger (serveur + jouer)"))
            Host();

        if (GUILayout.Button("Rejoindre"))
        {
            Client.ServerIp = _ip;
            Join();
        }
        GUILayout.EndArea();
    }

    private void HideMenu()
    {
        _started = true;
        if (MenuPanel != null) MenuPanel.SetActive(false);
    }

    private void SetStatus(string message)
    {
        Debug.LogWarning(message);
        if (StatusText != null) StatusText.text = message;
    }
}
