using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

namespace Mudatti.Posture
{
  /// <summary>
  ///   Runs <see cref="CrouchDetector"/> on every pose result and shows whether the person is crouching.
  /// </summary>
  public class CrouchMonitor : MonoBehaviour
  {
    [SerializeField] private PoseLandmarkerRunner runner;
    [SerializeField] private CrouchDetector crouchDetector;

    [Header("Debug overlay")]
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private Vector2 overlayPosition = new Vector2(10, 420);

    // Written on the detection thread, read on the main thread.
    private volatile bool _isCrouching;
    private volatile bool _hasResult;
    private GUIStyle _style;

    public bool IsCrouching => _isCrouching;

    private void OnEnable()
    {
      if (runner != null)
      {
        runner.OnPoseResult += HandlePoseResult;
      }
    }

    private void OnDisable()
    {
      if (runner != null)
      {
        runner.OnPoseResult -= HandlePoseResult;
      }
    }

    // May run on a background thread (LIVE_STREAM). IsCrouched only reads the result, so it is safe here.
    private void HandlePoseResult(PoseLandmarkerResult result)
    {
      _hasResult = result.poseLandmarks != null && result.poseLandmarks.Count > 0;
      _isCrouching = _hasResult && crouchDetector.IsCrouched(result);
    }

    private void OnGUI()
    {
      if (!showDebugOverlay)
      {
        return;
      }
      _style ??= new GUIStyle(GUI.skin.label) { fontSize = 22, richText = true };

      var text = !_hasResult ? "<color=grey>-</color>" : _isCrouching ? "<color=red>YES</color>" : "<color=lime>NO</color>";
      GUI.Box(new Rect(overlayPosition.x, overlayPosition.y, 260, 40), GUIContent.none);
      GUI.Label(new Rect(overlayPosition.x + 10, overlayPosition.y + 4, 250, 34), $"<b>Crouching:</b> {text}", _style);
    }
  }
}
