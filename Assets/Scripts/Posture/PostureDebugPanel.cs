using System.Collections.Generic;
using UnityEngine;

namespace Mudatti.Posture
{
  /// <summary>
  ///   Debug panel standing in for the game: switch modes with the mouse and watch slouch, crouch,
  ///   checkpoint progress and the events the game would receive.
  /// </summary>
  public class PostureDebugPanel : MonoBehaviour
  {
    private const int MaxEvents = 5;

    [SerializeField] private PostureTracker tracker;
    [SerializeField] private bool showModeButtons = true;
    [SerializeField] private float width = 430f;

    private readonly Queue<string> _events = new Queue<string>();
    private GUIStyle _label;
    private GUIStyle _button;

    private void OnEnable()
    {
      tracker.SlouchStarted += OnSlouchStarted;
      tracker.SlouchEnded += OnSlouchEnded;
      tracker.CheckpointCompleted += OnCheckpointCompleted;
      tracker.TrackingLost += OnTrackingLost;
      tracker.TrackingRestored += OnTrackingRestored;
      tracker.Crouch.Crouched += OnCrouched;
      tracker.Crouch.StoodUp += OnStoodUp;
    }

    private void OnDisable()
    {
      tracker.SlouchStarted -= OnSlouchStarted;
      tracker.SlouchEnded -= OnSlouchEnded;
      tracker.CheckpointCompleted -= OnCheckpointCompleted;
      tracker.TrackingLost -= OnTrackingLost;
      tracker.TrackingRestored -= OnTrackingRestored;
      tracker.Crouch.Crouched -= OnCrouched;
      tracker.Crouch.StoodUp -= OnStoodUp;
    }

    private void OnSlouchStarted() => Log("SlouchStarted");
    private void OnSlouchEnded() => Log("SlouchEnded");
    private void OnCheckpointCompleted() => Log("<color=lime>CheckpointCompleted</color>");
    private void OnTrackingLost() => Log("<color=orange>TrackingLost</color>");
    private void OnTrackingRestored() => Log("TrackingRestored");
    private void OnCrouched() => Log("Crouched");
    private void OnStoodUp() => Log("StoodUp");

    private void Log(string message)
    {
      _events.Enqueue($"{Time.time:0.0}s  {message}");
      while (_events.Count > MaxEvents)
      {
        _events.Dequeue();
      }
    }

    private void OnGUI()
    {
      _label ??= new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
      _button ??= new GUIStyle(GUI.skin.button) { fontSize = 18 };

      GUILayout.BeginArea(new Rect(UnityEngine.Screen.width - width - 10, 10, width, 520), GUI.skin.box);

      if (showModeButtons)
      {
        GUILayout.BeginHorizontal();
        foreach (PostureGameMode mode in System.Enum.GetValues(typeof(PostureGameMode)))
        {
          var label = tracker.Mode == mode ? $"[{mode}]" : mode.ToString();
          if (GUILayout.Button(label, _button))
          {
            tracker.SetMode(mode);
            Log($"SetMode({mode})");
          }
        }
        GUILayout.EndHorizontal();
      }

      var crouch = tracker.Crouch;
      GUILayout.Label($"<b>Mode:</b> {tracker.Mode}", _label);
      GUILayout.Label($"<b>Crouch:</b> {crouch.State}  (height {crouch.NormalizedHeight:0.00}{(crouch.HasCalibratedRatio ? "" : ", uncalibrated")})", _label);

      var slouch = tracker.IsSlouchActive
        ? $"{tracker.SlouchPercent:0}%{(tracker.IsSlouching ? "  <color=red>SLOUCHING</color>" : "")}"
        : $"<color=grey>{tracker.SlouchPercent:0}% (paused: {tracker.SlouchPauseReason})</color>";
      GUILayout.Label($"<b>Slouch:</b> {slouch}", _label);

      if (tracker.Mode == PostureGameMode.Checkpoint)
      {
        GUILayout.Label($"<b>Checkpoint:</b> {tracker.CheckpointStep}", _label);
        var bar = GUILayoutUtility.GetRect(width - 30, 18);
        GUI.Box(bar, GUIContent.none);
        GUI.Box(new Rect(bar.x, bar.y, bar.width * tracker.CheckpointProgress, bar.height), GUIContent.none, _button);
      }

      if (!string.IsNullOrEmpty(tracker.Prompt))
      {
        GUILayout.Label($"<b>Prompt:</b> <color=yellow>{tracker.Prompt}</color>", _label);
      }

      GUILayout.Space(8);
      GUILayout.Label("<b>Events</b>", _label);
      foreach (var e in _events)
      {
        GUILayout.Label(e, _label);
      }
      GUILayout.EndArea();
    }
  }
}
