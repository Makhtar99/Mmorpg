using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Gère le menu de pause en jeu.
/// - Appuyer sur Entrée ouvre / ferme le popup de pause.
/// - Le bouton "Quitter la session" ramène à l'écran de sélection de personnage.
/// 
/// == COMMENT CONFIGURER DANS UNITY ==
/// 1. Crée un Canvas (Screen Space - Overlay, Sort Order 10) nommé "PauseCanvas".
/// 2. Sous ce Canvas, crée un Panel nommé "PausePanel" (occupe tout l'écran,
///    couleur sombre semi-transparente, ex. RGBA 0,0,0,180).
/// 3. À l'intérieur du Panel, ajoute :
///      • Un Text / TextMeshPro  → "PAUSE"
///      • Un Button nommé "QuitButton" avec comme texte "Quitter la session"
/// 4. Attache ce script sur un GameObject vide dans la scène MetaVerse.
/// 5. Dans l'Inspector, glisse :
///      • pausePanel    → le GameObject "PausePanel"
///      • quitButton    → le composant Button de "QuitButton"
///      • homeSceneName → "CharacterSelectScene" (ou le nom exact de ta scène d'accueil)
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Le panneau de pause (Panel avec fond sombre + boutons)")]
    public GameObject pausePanel;

    [Tooltip("Bouton qui renvoie à l'écran d'accueil")]
    public Button quitButton;

    [Header("Scene Settings")]
    [Tooltip("Nom exact de la scène d'accueil (CharacterSelectScene)")]
    public string homeSceneName = "CharacterSelectScene";

    // ---------------------------------------------------------------
    private bool _isPaused = false;

    // ---------------------------------------------------------------
    void Start()
    {
        // S'assure que le panel est caché au départ
        if (pausePanel != null)
            pausePanel.SetActive(false);

        // Branche le bouton "Quitter la session"
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitSession);
        else
            Debug.LogWarning("[PauseMenuManager] quitButton non assigné dans l'Inspector !");

        if (pausePanel == null)
            Debug.LogWarning("[PauseMenuManager] pausePanel non assigné dans l'Inspector !");
    }

    // ---------------------------------------------------------------
    void Update()
    {
        // Détection de la touche Entrée (compatible New & Legacy Input System)
        bool enterPressed = Keyboard.current != null
            ? Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame
            : Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        if (enterPressed)
        {
            TogglePause();
        }
    }

    // ---------------------------------------------------------------
    /// <summary>Bascule l'état pause / jeu.</summary>
    public void TogglePause()
    {
        _isPaused = !_isPaused;
        ApplyPauseState();
    }

    /// <summary>Force l'ouverture du menu de pause.</summary>
    public void OpenPause()
    {
        _isPaused = true;
        ApplyPauseState();
    }

    /// <summary>Force la fermeture du menu de pause.</summary>
    public void ClosePause()
    {
        _isPaused = false;
        ApplyPauseState();
    }

    // ---------------------------------------------------------------
    private void ApplyPauseState()
    {
        if (pausePanel != null)
            pausePanel.SetActive(_isPaused);

        // Gèle / dégèle le temps de jeu
        Time.timeScale = _isPaused ? 0f : 1f;

        // Affiche / cache le curseur
        Cursor.visible   = _isPaused;
        Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;

        Debug.Log(_isPaused ? "[PauseMenuManager] Jeu en pause." : "[PauseMenuManager] Reprise du jeu.");
    }

    // ---------------------------------------------------------------
    /// <summary>Quitte la session et retourne à l'écran d'accueil.</summary>
    public void QuitSession()
    {
        Debug.Log("[PauseMenuManager] Quitter la session → " + homeSceneName);

        // Remet timeScale à 1 avant de changer de scène
        Time.timeScale   = 1f;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;

        // Nettoie les données de session si nécessaire
        // PlayerPrefs.DeleteKey("SelectedCharacter"); // ← décommente si tu veux forcer une re-sélection

        SceneManager.LoadScene(homeSceneName);
    }

    // ---------------------------------------------------------------
    void OnDestroy()
    {
        // Sécurité : s'assure que le temps repart si ce script est détruit
        Time.timeScale = 1f;
    }
}
