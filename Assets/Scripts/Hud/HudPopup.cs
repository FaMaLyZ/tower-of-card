using UnityEngine;
using UnityEngine.UI;

/// A panel the player opens from a HUD button and closes with Back (Run Info, Options).
/// Its full-screen backdrop catches clicks, so the table cannot be clicked while it is open.
/// It does not pause the game: every phase that could run on its own is short, and the others wait for input.
public class HudPopup : MonoBehaviour
{
    [Tooltip("The panel shown and hidden (usually a child, so this script keeps running)")]
    public GameObject panel;
    public Button backButton;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(Close);
        if (panel != null) panel.SetActive(false);
    }

    public void Open()
    {
        PanelSlideJuice.Show(panel, true);
        AudioManager.UISFX(SoundType.UI_Button);
    }

    public void Close()
    {
        PanelSlideJuice.Show(panel, false);
        AudioManager.UISFX(SoundType.UI_Button);
    }
}
