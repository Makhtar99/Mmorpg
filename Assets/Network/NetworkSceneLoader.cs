using System;
using UnityEngine.SceneManagement;

public static class NetworkSceneLoader
{
    public static Action<string> LoadScene = SceneManager.LoadScene;

    public static void Load(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        LoadScene(sceneName);
    }

    public static void Reset()
    {
        LoadScene = SceneManager.LoadScene;
    }
}
