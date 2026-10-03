using Mudatti.Posture;
using UnityEngine;

namespace Mudatti.Game
{
  /// <summary>
  ///   Tilts the character forward and slows it down while the player is slouching. The tilt is instant
  ///   on purpose, so the change is obvious while testing.
  /// </summary>
  public class SlouchLean : MonoBehaviour
  {
    [SerializeField] private PostureTracker tracker;
    [Tooltip("Optional; slowed down while leaning.")]
    [SerializeField] private SplineWalker walker;
    [SerializeField, Range(0.1f, 1f)] private float slouchSpeedFactor = 0.6f;
    [Tooltip("Rotated around X; place it at the character's feet.")]
    [SerializeField] private Transform leanPivot;
    [SerializeField, Range(0f, 100f)] private float slouchThreshold = 15f;
    [SerializeField] private float leanAngle = 45f;
    [Tooltip("Put the tracker in Running mode on start so slouching is scored.")]
    [SerializeField] private bool setRunningOnStart = true;

    public bool IsLeaning { get; private set; }
    /// <summary>Slouch percentage at which the character leans and slows down.</summary>
    public float SlouchThreshold => slouchThreshold;

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
      if (walker != null)
      {
        walker.SpeedMultiplier = IsLeaning ? slouchSpeedFactor : 1f;
      }
    }
  }
}
