using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadMainGameplay()
    {
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
        SceneManager.LoadScene("MainGame");
    }

    public void LoadMainMenu()
    {
        AudioManager.Instance.StopSFX();

        SceneManager.LoadScene("MainMenu");
    }
    public void ExitGame()
    {
        AudioManager.Instance.PlayOneShotUISFX(SoundType.UI_Button);
        Application.Quit();
    }
}