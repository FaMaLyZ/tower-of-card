using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    /// The scenes the game uses (must be in File > Build Profiles). The old design: "old MainMenu", "MainGame".
    public const string MenuScene = "MainMenu";
    public const string GameplayScene = "Pound";

    public void LoadMainGameplay()
    {
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
        SceneManager.LoadScene(GameplayScene);
    }

    public void LoadMainMenu()
    {
        AudioManager.Instance.StopSFX();
        SceneManager.LoadScene(MenuScene);
    }
    public void ExitGame()
    {
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
        Application.Quit();
    }
}
