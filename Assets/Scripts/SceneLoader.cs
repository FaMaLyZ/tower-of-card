using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadMainGameplay()
    {
        AudioManager.UISFX(SoundType.UI_Button);
        SceneManager.LoadScene("MainGame");
    }

    public void LoadMainMenu()
    {
        AudioManager.SFXStop();
        SceneManager.LoadScene("MainMenu");
    }
    public void ExitGame()
    {
        AudioManager.UISFX(SoundType.UI_Button);
        Application.Quit();
    }
}