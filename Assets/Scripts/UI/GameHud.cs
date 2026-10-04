using Mudatti.Game;
using Mudatti.Posture;
using UnityEngine;
using UnityEngine.UI;

namespace Mudatti.UI
{
  /// <summary>
  ///   Gameplay HUD: path progress, collected cats and the slouch meter. The meter is blue below the slouch
  ///   threshold and red above it, with the threshold at a fixed height. Gameplay references left empty
  ///   are looked up in the scene, and a widget whose source is missing is hidden.
  /// </summary>
  public class GameHud : MonoBehaviour
  {
    [Header("Game (found in the scene when empty)")]
    [SerializeField] private PostureTracker tracker;
    [SerializeField] private SlouchLean slouchLean;
    [SerializeField] private SplineWalker walker;
    [SerializeField] private CrouchStops stops;

    [Header("Progress")]
    [SerializeField] private GameObject progressRoot;
    [Tooltip("Image with Type = Filled (Horizontal); the trace behind the runner.")]
    [SerializeField] private Image progressFill;
    [SerializeField] private RectTransform runner;

    [Header("Cats")]
    [SerializeField] private GameObject catRoot;
    [SerializeField] private Text catText;

    [Header("Slouch meter")]
    [SerializeField] private GameObject meterRoot;
    [Tooltip("Fraction of the meter (from the bottom) that is blue; the threshold sits at this height.")]
    [SerializeField, Range(0.05f, 0.95f)] private float thresholdPosition = 0.7f;
    [Tooltip("Spans from the bottom of the meter to the threshold position.")]
    [SerializeField] private RectTransform blueZone;
    [Tooltip("Spans from the threshold position to the top of the meter.")]
    [SerializeField] private RectTransform redZone;
    [SerializeField] private RectTransform ball;
    [SerializeField] private Image ballImage;
    [SerializeField] private float ballSmoothingSeconds = 0.15f;
    [Tooltip("Ball alpha while slouching is not being scored (crouching, at a stop, ...).")]
    [SerializeField, Range(0f, 1f)] private float inactiveBallAlpha = 0.35f;

    private float _ballValue;

    private void Awake()
    {
      if (tracker == null) tracker = FindFirstObjectByType<PostureTracker>();
      if (slouchLean == null) slouchLean = FindFirstObjectByType<SlouchLean>();
      if (walker == null) walker = FindFirstObjectByType<SplineWalker>();
      if (stops == null) stops = FindFirstObjectByType<CrouchStops>();

      progressRoot.SetActive(walker != null);
      catRoot.SetActive(stops != null);
      meterRoot.SetActive(tracker != null);
    }

    private void Update()
    {
      if (walker != null)
      {
        var progress = walker.Length > 0f ? Mathf.Clamp01(walker.Distance / walker.Length) : 0f;
        progressFill.fillAmount = progress;
        SetAnchorX(runner, progress);
      }

      if (stops != null)
      {
        catText.text = $"{stops.CatsCollected}/{stops.CatsTotal}";
      }

      if (tracker != null)
      {
        UpdateMeter();
      }
    }

    private void UpdateMeter()
    {
      // Without a SlouchLean in the scene, fall back to the detector's own threshold.
      var threshold = slouchLean != null ? slouchLean.SlouchThreshold : tracker.Slouch.SlouchThreshold;
      blueZone.anchorMax = new Vector2(blueZone.anchorMax.x, thresholdPosition);
      redZone.anchorMin = new Vector2(redZone.anchorMin.x, thresholdPosition);

      var target = MeterPosition(tracker.SlouchPercent, threshold, BallSplit());
      var alpha = ballSmoothingSeconds > 0f ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / ballSmoothingSeconds) : 1f;
      _ballValue = Mathf.Lerp(_ballValue, target, alpha);
      SetAnchorY(ball, _ballValue);

      var color = ballImage.color;
      color.a = tracker.IsSlouchActive ? 1f : inactiveBallAlpha;
      ballImage.color = color;
    }

    /// <summary>
    ///   Maps a slouch percentage onto the meter: 0..threshold fills the blue part up to <paramref name="split" />,
    ///   threshold..100 fills the red part above it.
    /// </summary>
    public static float MeterPosition(float percent, float threshold, float split)
    {
      threshold = Mathf.Clamp(threshold, 0.01f, 99.99f);
      return percent <= threshold
        ? Mathf.Lerp(0f, split, percent / threshold)
        : Mathf.Lerp(split, 1f, (percent - threshold) / (100f - threshold));
    }

    /// <summary>
    ///   The blue/red boundary in the ball's own range. The ball travels in a smaller area than the zones,
    ///   so the boundary is converted to keep the ball on the colour line exactly at the threshold.
    /// </summary>
    private float BallSplit()
    {
      var area = (RectTransform)ball.parent;
      var corners = new Vector3[4];
      redZone.GetWorldCorners(corners);
      var boundaryY = area.InverseTransformPoint(corners[0]).y;
      var rect = area.rect;
      return rect.height > 0f ? Mathf.Clamp01(Mathf.InverseLerp(rect.yMin, rect.yMax, boundaryY)) : thresholdPosition;
    }

    private static void SetAnchorX(RectTransform rect, float x)
    {
      rect.anchorMin = new Vector2(x, rect.anchorMin.y);
      rect.anchorMax = new Vector2(x, rect.anchorMax.y);
    }

    private static void SetAnchorY(RectTransform rect, float y)
    {
      rect.anchorMin = new Vector2(rect.anchorMin.x, y);
      rect.anchorMax = new Vector2(rect.anchorMax.x, y);
    }
  }
}
