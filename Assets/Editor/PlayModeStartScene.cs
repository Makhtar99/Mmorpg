#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PlayModeStartScene
{
    const string GameMenuScenePath = "Assets/Scenes/GameMenu.unity";

    static PlayModeStartScene()
    {
        EditorApplication.delayCall += Configure;
    }

    static void Configure()
    {
        SceneAsset gameMenuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameMenuScenePath);
        if (gameMenuScene == null)
        {
            return;
        }

        EditorSceneManager.playModeStartScene = gameMenuScene;
    }
}
#endif
