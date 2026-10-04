using Mudatti.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mudatti.UI
{
  /// <summary>
  ///   Main menu actions. Hook the buttons' onClick to <see cref="StartGame"/>, <see cref="OpenSettings"/>,
  ///   <see cref="CloseSettings"/> and <see cref="Quit"/>.
  /// </summary>
  public class IntroMenu : MonoBehaviour
  {
    [SerializeField] private string startScene = "Calibration";
    [SerializeField] private GameObject mainButtons;
    [SerializeField] private GameObject settingsPanel;

    private void Awake()
    {
      settingsPanel.SetActive(false);
      mainButtons.SetActive(true);
    }

    public void StartGame() => SceneManager.LoadScene(startScene);

    // The menu buttons are hidden while the panel is open so the hand cursor can't press them through it.
    public void OpenSettings()
    {
      mainButtons.SetActive(false);
      settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
      settingsPanel.SetActive(false);
      mainButtons.SetActive(true);
    }

    public void Quit() => GameFlow.Quit();
  }
}
