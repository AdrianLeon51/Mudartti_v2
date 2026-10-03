using Mudatti.Game;
using Mudatti.Posture;
using UnityEngine;
using UnityEngine.UI;

namespace Mudatti.UI
{
  /// <summary>
  ///   Gameplay HUD: path progress, collected cats and the slouch meter. Gameplay references left empty
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
    [Tooltip("Spans from the threshold to the top of the meter.")]
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
      redZone.anchorMin = new Vector2(redZone.anchorMin.x, Mathf.Clamp01(threshold / 100f));

      var target = Mathf.Clamp01(tracker.SlouchPercent / 100f);
      var alpha = ballSmoothingSeconds > 0f ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / ballSmoothingSeconds) : 1f;
      _ballValue = Mathf.Lerp(_ballValue, target, alpha);
      SetAnchorY(ball, _ballValue);

      var color = ballImage.color;
      color.a = tracker.IsSlouchActive ? 1f : inactiveBallAlpha;
      ballImage.color = color;
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
