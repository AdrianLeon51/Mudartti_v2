using Mudatti.Posture;
using UnityEngine;

namespace Mudatti.Game
{
  /// <summary>
  ///   Tilts the character forward while the player is slouching. The switch is instant on purpose,
  ///   so the change is obvious while testing.
  /// </summary>
  public class SlouchLean : MonoBehaviour
  {
    [SerializeField] private PostureTracker tracker;
    [Tooltip("Rotated around X; place it at the character's feet.")]
    [SerializeField] private Transform leanPivot;
    [SerializeField, Range(0f, 100f)] private float slouchThreshold = 15f;
    [SerializeField] private float leanAngle = 45f;
    [Tooltip("Put the tracker in Running mode on start so slouching is scored.")]
    [SerializeField] private bool setRunningOnStart = true;

    public bool IsLeaning { get; private set; }

    private void Start()
    {
      if (setRunningOnStart)
      {
        tracker.SetMode(PostureGameMode.Running);
      }
    }

    private void Update()
    {
      // While slouch scoring is paused (crouching, settling, tracking lost) the character stays upright.
      IsLeaning = tracker.IsSlouchActive && tracker.SlouchPercent > slouchThreshold;
      leanPivot.localRotation = Quaternion.Euler(IsLeaning ? leanAngle : 0f, 0f, 0f);
    }
  }
}
