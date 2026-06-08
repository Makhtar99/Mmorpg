using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterSelectManager : MonoBehaviour
{
    void Awake()
    {
        BindButton("Button Engineer", SelectEngineer);
        BindButton("Button Knight", SelectKnight);
        BindButton("Button Rogue", SelectRogue);
        BindButton("Button Mage", SelectMage);
        BindButton("Button Barbarian", SelectBarbarian);
        BindButton("Button Druid", SelectDruid);
    }

    public void SelectEngineer()  { ChooseAndLoad("engineer");  }
    public void SelectKnight()    { ChooseAndLoad("knight");    }
    public void SelectRogue()     { ChooseAndLoad("rogue");     }
    public void SelectMage()      { ChooseAndLoad("mage");      }
    public void SelectBarbarian() { ChooseAndLoad("barbarian"); }
    public void SelectDruid()     { ChooseAndLoad("druid");     }

    void BindButton(string buttonName, UnityEngine.Events.UnityAction action)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button.gameObject.name != buttonName)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            return;
        }

        Debug.LogWarning($"Bouton introuvable: {buttonName}");
    }

    void ChooseAndLoad(string characterName)
    {
        PlayerPrefs.SetString("SelectedCharacter", characterName);
        PlayerPrefs.Save();
        Debug.Log("Chargement avec : " + characterName);
        SceneManager.LoadScene("MetaVerse");
    }
}