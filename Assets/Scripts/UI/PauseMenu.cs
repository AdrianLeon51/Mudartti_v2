using Mudatti.Game;
using UnityEngine;

namespace Mudatti.UI
{
  /// <summary>
  ///   Freezes the game (Time.timeScale = 0) and shows the pause panel. Hook the buttons' onClick to
  ///   <see cref="Pause"/>, <see cref="Resume"/> and <see cref="Quit"/>.
  /// </summary>
  public class PauseMenu : MonoBehaviour
  {
    [SerializeField] private GameObject pauseButton;
    [SerializeField] private GameObject pausePanel;

    public bool IsPaused { get; private set; }

    private void Awake()
    {
      pausePanel.SetActive(false);
    }

    public void Pause()
    {
      IsPaused = true;
      Time.timeScale = 0f;
      pausePanel.SetActive(true);
      // Hidden so the hand cursor resting on it can't trigger it again.
      pauseButton.SetActive(false);
    }

    public void Resume()
    {
      IsPaused = false;
      Time.timeScale = 1f;
      pausePanel.SetActive(false);
      pauseButton.SetActive(true);
    }

    public void Quit()
    {
      Time.timeScale = 1f;
      GameFlow.Quit();
    }

    private void OnDestroy()
    {
      if (IsPaused)
      {
        Time.timeScale = 1f;
      }
    }
  }
}
