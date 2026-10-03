using System;
using UnityEngine;

namespace Mudatti.Posture
{
  /// <summary>
  ///   Estimates how much a person standing in front of the camera is slouching, as a percentage
  ///   relative to their own calibrated upright posture. Shows a debug overlay with the breakdown.
  /// </summary>
  public class SlouchDetector : MonoBehaviour
  {
    // BlazePose landmark indices
    private const int LeftEar = 7, RightEar = 8;
    private const int LeftShoulder = 11, RightShoulder = 12;
    private const int LeftHip = 23, RightHip = 24;
    private const int LeftAnkle = 27, RightAnkle = 28;

    private const int HeadDrop = 0, TorsoShortening = 1, HeadForward = 2, TrunkLean = 3;
    private const int MetricCount = 4;
    private static readonly string[] MetricNames = { "Head drop", "Torso shortening", "Head forward (3D)", "Trunk lean (3D)" };
    // +1: metric grows when slouching, -1: metric shrinks when slouching
    private static readonly float[] SlouchDirection = { -1f, -1f, 1f, 1f };

    [SerializeField] private PoseLandmarkFeed feed;

    [Header("Validity")]
    [SerializeField, Range(0f, 1f)] private float minVisibility = 0.5f;
    [SerializeField] private bool requireFullBody = true;
    [Tooltip("Max |left shoulder z - right shoulder z| / shoulder width before the person counts as turned away.")]
    [SerializeField] private float maxShoulderDepthRatio = 0.6f;

    [Header("Calibration")]
    [Tooltip("Start calibrating as soon as a valid pose is seen. Disable to wait for StartCalibration().")]
    [SerializeField] private bool autoCalibrate = true;
    [SerializeField] private float calibrationSeconds = 3f;
    [Tooltip("Frames whose leg ratio (hip-to-ankle / shoulder-to-hip) is below this are skipped while calibrating, so bent knees don't spoil the baseline.")]
    [SerializeField] private float minCalibrationLegRatio = 1.2f;

    [Header("Scoring (index: head drop, torso, head forward, trunk lean)")]
    [Tooltip("Change from baseline (in shoulder widths) that counts as 100% slouch for each metric.")]
    [SerializeField] private float[] maxDelta = { 0.35f, 0.15f, 0.4f, 0.3f };
    [SerializeField] private float[] weights = { 0.4f, 0.25f, 0.2f, 0.15f };
    [SerializeField] private float smoothingSeconds = 0.5f;

    [Header("Slouching flag")]
    [SerializeField, Range(0f, 100f)] private float slouchThreshold = 35f;
    [SerializeField] private float slouchHoldSeconds = 2f;

    [Header("Debug overlay")]
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private bool showRecalibrateButton = true;

    private readonly float[] _raw = new float[MetricCount];
    private readonly float[] _baseline = new float[MetricCount];
    private readonly float[] _calibrationSum = new float[MetricCount];
    private readonly float[] _percent = new float[MetricCount];
    private float _legRatio = -1f;
    private float _calibrationElapsed;
    private bool _isCalibrated;
    private bool _isCalibrating;

    private float _overallPercent;
    private float _lastSampleTime = -1f;
    private float _aboveThresholdSince = -1f;
    private string _status = "Waiting for camera";
    private GUIStyle _style;

    public event Action CalibrationStarted;
    public event Action Calibrated;

    /// <summary>While paused the score is not updated: the percentage holds and the slouching flag clears.</summary>
    public bool Paused { get; set; }
    public float[] Baseline => (float[])_baseline.Clone();
    public float SlouchPercent => _overallPercent;
    public float SlouchThreshold => slouchThreshold;
    public bool IsCalibrated => _isCalibrated;
    public bool IsCalibrating => _isCalibrating;
    public float CalibrationProgress => calibrationSeconds > 0f ? Mathf.Clamp01(_calibrationElapsed / calibrationSeconds) : 1f;
    public bool IsSlouching => _isCalibrated && _aboveThresholdSince >= 0f && Time.time - _aboveThresholdSince >= slouchHoldSeconds;
    public string Status => _status;

    private void OnEnable()
    {
      if (feed != null)
      {
        feed.FrameUpdated += HandleFrame;
      }
    }

    private void OnDisable()
    {
      if (feed != null)
      {
        feed.FrameUpdated -= HandleFrame;
      }
    }

    /// <summary>Discards the current baseline and records a new one from the next valid frames.</summary>
    public void StartCalibration()
    {
      _isCalibrated = false;
      _isCalibrating = true;
      _calibrationElapsed = 0f;
      Array.Clear(_calibrationSum, 0, MetricCount);
      Array.Clear(_percent, 0, MetricCount);
      _overallPercent = 0f;
      _aboveThresholdSince = -1f;
      CalibrationStarted?.Invoke();
    }

    /// <summary>Restores a previously saved baseline (see <see cref="PostureCalibrationData"/>).</summary>
    public void ApplyBaseline(float[] baseline)
    {
      if (baseline == null || baseline.Length != MetricCount)
      {
        return;
      }
      Array.Copy(baseline, _baseline, MetricCount);
      Array.Clear(_percent, 0, MetricCount);
      _overallPercent = 0f;
      _aboveThresholdSince = -1f;
      _isCalibrating = false;
      _isCalibrated = true;
    }

    private void HandleFrame(PoseFrame frame)
    {
      var now = Time.time;
      var dt = _lastSampleTime < 0f ? 0f : now - _lastSampleTime;
      _lastSampleTime = now;

      if (!frame.HasPose)
      {
        _status = "No person";
        return;
      }
      if (!TryComputeMetrics(frame, out _status))
      {
        return;
      }

      if (!_isCalibrated)
      {
        if (!_isCalibrating && autoCalibrate)
        {
          StartCalibration();
        }
        if (_isCalibrating)
        {
          if (_legRatio >= 0f && _legRatio < minCalibrationLegRatio)
          {
            _status = "Calibrating - stand up straight";
            return;
          }
          Calibrate(dt);
        }
        else
        {
          _status = "Waiting to calibrate";
        }
        return;
      }

      if (Paused)
      {
        _aboveThresholdSince = -1f;
        _status = "Paused";
        return;
      }

      Score(dt, now);
      _status = "Tracking";
    }

    private bool TryComputeMetrics(PoseFrame frame, out string status)
    {
      bool Visible(int i) => frame.IsVisible(i, minVisibility);

      if (!Visible(LeftEar) || !Visible(RightEar) || !Visible(LeftShoulder) || !Visible(RightShoulder)
        || !Visible(LeftHip) || !Visible(RightHip))
      {
        status = "Upper body not visible";
        return false;
      }
      if (requireFullBody && (!Visible(LeftAnkle) || !Visible(RightAnkle)))
      {
        status = "Not full body (ankles hidden)";
        return false;
      }

      var world = frame.World;
      var worldShoulderWidth = Vector3.Distance(world[LeftShoulder], world[RightShoulder]);
      if (worldShoulderWidth < 1e-4f
        || Mathf.Abs(world[LeftShoulder].z - world[RightShoulder].z) / worldShoulderWidth > maxShoulderDepthRatio)
      {
        status = "Not facing camera";
        return false;
      }

      var px = frame.Pixel;
      var shoulderWidth = Vector2.Distance(px[LeftShoulder], px[RightShoulder]);
      if (shoulderWidth < 1f)
      {
        status = "Shoulders too close together";
        return false;
      }

      var earMid = (px[LeftEar] + px[RightEar]) * 0.5f;
      var shoulderMid = (px[LeftShoulder] + px[RightShoulder]) * 0.5f;
      var hipMid = (px[LeftHip] + px[RightHip]) * 0.5f;
      var earMidZ = (world[LeftEar].z + world[RightEar].z) * 0.5f;
      var shoulderMidZ = (world[LeftShoulder].z + world[RightShoulder].z) * 0.5f;
      var hipMidZ = (world[LeftHip].z + world[RightHip].z) * 0.5f;

      // Image y grows downwards; world z is smaller when closer to the camera.
      _raw[HeadDrop] = (shoulderMid.y - earMid.y) / shoulderWidth;
      _raw[TorsoShortening] = (hipMid.y - shoulderMid.y) / shoulderWidth;
      _raw[HeadForward] = (shoulderMidZ - earMidZ) / worldShoulderWidth;
      _raw[TrunkLean] = (hipMidZ - shoulderMidZ) / worldShoulderWidth;

      // Leg ratio, used only to reject bent-knee frames during calibration (-1 when ankles aren't visible).
      _legRatio = Visible(LeftAnkle) && Visible(RightAnkle) && hipMid.y - shoulderMid.y > 1f
        ? ((px[LeftAnkle].y + px[RightAnkle].y) * 0.5f - hipMid.y) / (hipMid.y - shoulderMid.y)
        : -1f;

      status = null;
      return true;
    }

    private void Calibrate(float dt)
    {
      for (var i = 0; i < MetricCount; i++)
      {
        _calibrationSum[i] += _raw[i] * dt;
      }
      _calibrationElapsed += dt;
      _status = $"Calibrating - stand up straight ({CalibrationProgress * 100f:0}%)";

      if (_calibrationElapsed >= calibrationSeconds)
      {
        for (var i = 0; i < MetricCount; i++)
        {
          _baseline[i] = _calibrationSum[i] / _calibrationElapsed;
        }
        _isCalibrated = true;
        _isCalibrating = false;
        Calibrated?.Invoke();
      }
    }

    private void Score(float dt, float now)
    {
      float weighted = 0f, weightSum = 0f;
      for (var i = 0; i < MetricCount; i++)
      {
        var delta = (_raw[i] - _baseline[i]) * SlouchDirection[i];
        var range = i < maxDelta.Length && maxDelta[i] > 0f ? maxDelta[i] : 1f;
        _percent[i] = Mathf.Clamp01(delta / range) * 100f;

        var weight = i < weights.Length ? Mathf.Max(0f, weights[i]) : 0f;
        weighted += _percent[i] * weight;
        weightSum += weight;
      }
      var target = weightSum > 0f ? weighted / weightSum : 0f;

      var alpha = smoothingSeconds > 0f ? 1f - Mathf.Exp(-dt / smoothingSeconds) : 1f;
      _overallPercent = Mathf.Lerp(_overallPercent, target, alpha);

      if (_overallPercent >= slouchThreshold)
      {
        if (_aboveThresholdSince < 0f)
        {
          _aboveThresholdSince = now;
        }
      }
      else
      {
        _aboveThresholdSince = -1f;
      }
    }

    private void OnGUI()
    {
      if (!showDebugOverlay)
      {
        return;
      }
      _style ??= new GUIStyle(GUI.skin.label) { fontSize = 22, richText = true };
      var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };

      GUILayout.BeginArea(new Rect(10, 10, 560, 400), GUI.skin.box);
      GUILayout.Label($"<b>Status:</b> {_status}", _style);

      if (_isCalibrated)
      {
        var color = IsSlouching ? "red" : "lime";
        GUILayout.Label($"<b>Slouch: <color={color}>{_overallPercent:0}%</color></b>{(IsSlouching ? "  <color=red>SLOUCHING</color>" : "")}", _style);
        for (var i = 0; i < MetricCount; i++)
        {
          GUILayout.Label($"{MetricNames[i]}: {_percent[i]:0}%   (now {_raw[i]:0.00} / base {_baseline[i]:0.00})", _style);
        }
      }

      if (showRecalibrateButton && GUILayout.Button("Recalibrate", buttonStyle))
      {
        StartCalibration();
      }
      GUILayout.EndArea();
    }
  }
}
