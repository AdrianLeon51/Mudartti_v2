using UnityEngine;

namespace Mudatti.Game
{
  public static class GameFlow
  {
    /// <summary>Closes the game, or leaves Play mode in the editor.</summary>
    public static void Quit()
    {
#if UNITY_EDITOR
      UnityEditor.EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
    }
  }
}
