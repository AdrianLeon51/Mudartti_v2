using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mudatti.Game
{
    public static class GameFlow
    {
        /// <summary>Closes the game, or leaves Play mode in the editor.</summary>
        public static void Quit(CrouchStops _crouchStops, GameObject finalText, Text resultTextMesh)
        {
            finalText.SetActive(true);
            
            if (_crouchStops.CatsCollected < _crouchStops.CatsTotal)
            {
                resultTextMesh.text = "Don't give up!";
            }
            else
            {
                resultTextMesh.text = "Congratulations!";
            }
            
            //wait 5 seconds
            
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}