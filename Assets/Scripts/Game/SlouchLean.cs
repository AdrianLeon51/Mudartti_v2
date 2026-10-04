using Mudatti.Posture;
using UnityEngine;

namespace Mudatti.Game
{
  /// <summary>
  ///   Switches the character to its sad walk and slows it down while the player is slouching. The switch
  ///   is immediate on purpose, so the change is obvious while testing.
  /// </summary>
  public class SlouchLean : MonoBehaviour
  {
    private static readonly int SlouchingParam = Animator.StringToHash("Slouching");

    [SerializeField] private PostureTracker tracker;
    [Tooltip("Optional; slowed down while slouching.")]
    [SerializeField] private SplineWalker walker;
    [SerializeField, Range(0.1f, 1f)] private float slouchSpeedFactor = 0.6f;
    [Tooltip("The character's animator; needs a \"Slouching\" bool parameter.")]
    [SerializeField] private Animator animator;
    [SerializeField, Range(0f, 100f)] private float slouchThreshold = 15f;
    [Tooltip("Put the tracker in Running mode on start so slouching is scored.")]
    [SerializeField] private bool setRunningOnStart = true;

    public bool IsLeaning { get; private set; }
    /// <summary>Slouch percentage at which the character switches to the sad walk and slows down.</summary>
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
      // While slouch scoring is paused (crouching, settling, tracking lost) the character walks normally.
      IsLeaning = tracker.IsSlouchActive && tracker.SlouchPercent > slouchThreshold;
      animator.SetBool(SlouchingParam, IsLeaning);
      if (walker != null)
      {
        walker.SpeedMultiplier = IsLeaning ? slouchSpeedFactor : 1f;
      }

      if (IsLeaning)
      {
        //starts losing cats each 2 seconds
      }
      else
      {
        //stop losing cats
      }
    }
  }
}
