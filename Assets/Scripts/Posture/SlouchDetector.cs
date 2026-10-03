using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
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
    private const int LandmarkCount = 33;

    private const int HeadDrop = 0, TorsoShortening = 1, HeadForward = 2, TrunkLean = 3;
    private const int MetricCount = 4;
    private static readonly string[] MetricNames = { "Head drop", "Torso shortening", "Head forward (3D)", "Trunk lean (3D)" };
    // +1: metric grows when slouching, -1: metric shrinks when slouching
    private static readonly float[] SlouchDirection = { -1f, -1f, 1f, 1f };

    [SerializeField] private PoseLandmarkerRunner runner;

    [Header("Validity")]
    [SerializeField, Range(0f, 1f)] private float minVisibility = 0.5f;
    [SerializeField] private bool requireFullBody = true;
    [Tooltip("Max |left shoulder z - right shoulder z| / shoulder width before the person counts as turned away.")]
    [SerializeField] private float maxShoulderDepthRatio = 0.6f;

    [Header("Calibration")]
    [SerializeField] private float calibrationSeconds = 3f;

    [Header("Scoring (index: head drop, torso, head forward, trunk lean)")]
    [Tooltip("Change from baseline (in shoulder widths) that counts as 100% slouch for each metric.")]
    [SerializeField] private float[] maxDelta = { 0.35f, 0.15f, 0.4f, 0.3f };
    [SerializeField] private float[] weights = { 0.4f, 0.25f, 0.2f, 0.15f };
    [SerializeField] private float smoothingSeconds = 0.5f;

    [Header("Slouching flag")]
    [SerializeField, Range(0f, 100f)] private float slouchThreshold = 35f;
    [SerializeField] private float slouchHoldSeconds = 2f;

    // Written by the detection thread, read on the main thread under _lock.
    private readonly object _lock = new object();
    private readonly Vector3[] _sharedImage = new Vector3[LandmarkCount]; // x, y normalized; z = visibility
    private readonly Vector3[] _sharedWorld = new Vector3[LandmarkCount];
    private bool _hasNewFrame;
    private bool _sharedHasPose;

    private readonly Vector3[] _image = new Vector3[LandmarkCount];
    private readonly Vector3[] _world = new Vector3[LandmarkCount];

    private readonly float[] _raw = new float[MetricCount];
    private readonly float[] _baseline = new float[MetricCount];
    private readonly float[] _calibrationSum = new float[MetricCount];
    private readonly float[] _percent = new float[MetricCount];
    private float _calibrationElapsed;
    private bool _isCalibrated;

    private float _overallPercent;
    private float _lastSampleTime = -1f;
    private float _aboveThresholdSince = -1f;
    private string _status = "Waiting for camera";
    private GUIStyle _style;

    public float SlouchPercent => _overallPercent;
    public bool IsCalibrated => _isCalibrated;

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

    public void Recalibrate()
    {
      _isCalibrated = false;
      _calibrationElapsed = 0f;
      System.Array.Clear(_calibrationSum, 0, MetricCount);
      System.Array.Clear(_percent, 0, MetricCount);
      _overallPercent = 0f;
      _aboveThresholdSince = -1f;
    }

    // May run on a background thread (LIVE_STREAM), so only copy data here.
    private void HandlePoseResult(PoseLandmarkerResult result)
    {
      lock (_lock)
      {
        _hasNewFrame = true;
        _sharedHasPose = result.poseLandmarks != null && result.poseLandmarks.Count > 0
          && result.poseWorldLandmarks != null && result.poseWorldLandmarks.Count > 0
          && result.poseLandmarks[0].landmarks.Count >= LandmarkCount
          && result.poseWorldLandmarks[0].landmarks.Count >= LandmarkCount;
        if (!_sharedHasPose)
        {
          return;
        }

        var image = result.poseLandmarks[0].landmarks;
        var world = result.poseWorldLandmarks[0].landmarks;
        for (var i = 0; i < LandmarkCount; i++)
        {
          _sharedImage[i] = new Vector3(image[i].x, image[i].y, image[i].visibility ?? 1f);
          _sharedWorld[i] = new Vector3(world[i].x, world[i].y, world[i].z);
        }
      }
    }

    private void Update()
    {
      bool hasPose;
      lock (_lock)
      {
        if (!_hasNewFrame)
        {
          return;
        }
        _hasNewFrame = false;
        hasPose = _sharedHasPose;
        if (hasPose)
        {
          System.Array.Copy(_sharedImage, _image, LandmarkCount);
          System.Array.Copy(_sharedWorld, _world, LandmarkCount);
        }
      }

      var now = Time.time;
      var dt = _lastSampleTime < 0f ? 0f : now - _lastSampleTime;
      _lastSampleTime = now;

      if (!hasPose)
      {
        _status = "No person";
        return;
      }
      if (!TryComputeMetrics(out _status))
      {
        return;
      }

      if (!_isCalibrated)
      {
        Calibrate(dt);
        return;
      }

      Score(dt, now);
      _status = "Tracking";
    }

    private bool TryComputeMetrics(out string status)
    {
      if (!IsVisible(LeftEar) || !IsVisible(RightEar) || !IsVisible(LeftShoulder) || !IsVisible(RightShoulder)
        || !IsVisible(LeftHip) || !IsVisible(RightHip))
      {
        status = "Upper body not visible";
        return false;
      }
      if (requireFullBody && (!IsVisible(LeftAnkle) || !IsVisible(RightAnkle)))
      {
        status = "Not full body (ankles hidden)";
        return false;
      }

      var worldShoulderWidth = Vector3.Distance(_world[LeftShoulder], _world[RightShoulder]);
      if (worldShoulderWidth < 1e-4f
        || Mathf.Abs(_world[LeftShoulder].z - _world[RightShoulder].z) / worldShoulderWidth > maxShoulderDepthRatio)
      {
        status = "Not facing camera";
        return false;
      }

      // Normalized image coords are scaled per axis, so convert to pixels before comparing distances.
      var source = ImageSourceProvider.ImageSource;
      var width = source != null && source.textureWidth > 0 ? source.textureWidth : 1f;
      var height = source != null && source.textureHeight > 0 ? source.textureHeight : 1f;
      Vector2 Px(int i) => new Vector2(_image[i].x * width, _image[i].y * height);

      var shoulderWidth = Vector2.Distance(Px(LeftShoulder), Px(RightShoulder));
      if (shoulderWidth < 1f)
      {
        status = "Shoulders too close together";
        return false;
      }

      var earMid = (Px(LeftEar) + Px(RightEar)) * 0.5f;
      var shoulderMid = (Px(LeftShoulder) + Px(RightShoulder)) * 0.5f;
      var hipMid = (Px(LeftHip) + Px(RightHip)) * 0.5f;
      var earMidZ = (_world[LeftEar].z + _world[RightEar].z) * 0.5f;
      var shoulderMidZ = (_world[LeftShoulder].z + _world[RightShoulder].z) * 0.5f;
      var hipMidZ = (_world[LeftHip].z + _world[RightHip].z) * 0.5f;

      // Image y grows downwards; world z is smaller when closer to the camera.
      _raw[HeadDrop] = (shoulderMid.y - earMid.y) / shoulderWidth;
      _raw[TorsoShortening] = (hipMid.y - shoulderMid.y) / shoulderWidth;
      _raw[HeadForward] = (shoulderMidZ - earMidZ) / worldShoulderWidth;
      _raw[TrunkLean] = (hipMidZ - shoulderMidZ) / worldShoulderWidth;

      status = null;
      return true;
    }

    private bool IsVisible(int index) => _image[index].z >= minVisibility;

    private void Calibrate(float dt)
    {
      for (var i = 0; i < MetricCount; i++)
      {
        _calibrationSum[i] += _raw[i] * dt;
      }
      _calibrationElapsed += dt;
      _status = $"Calibrating - stand up straight ({Mathf.Clamp01(_calibrationElapsed / calibrationSeconds) * 100f:0}%)";

      if (_calibrationElapsed >= calibrationSeconds)
      {
        for (var i = 0; i < MetricCount; i++)
        {
          _baseline[i] = _calibrationSum[i] / _calibrationElapsed;
        }
        _isCalibrated = true;
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
      _style ??= new GUIStyle(GUI.skin.label) { fontSize = 22, richText = true };
      var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };

      GUILayout.BeginArea(new Rect(10, 10, 560, 400), GUI.skin.box);
      GUILayout.Label($"<b>Status:</b> {_status}", _style);

      if (_isCalibrated)
      {
        var isSlouching = _aboveThresholdSince >= 0f && Time.time - _aboveThresholdSince >= slouchHoldSeconds;
        var color = isSlouching ? "red" : "lime";
        GUILayout.Label($"<b>Slouch: <color={color}>{_overallPercent:0}%</color></b>{(isSlouching ? "  <color=red>SLOUCHING</color>" : "")}", _style);
        for (var i = 0; i < MetricCount; i++)
        {
          GUILayout.Label($"{MetricNames[i]}: {_percent[i]:0}%   (now {_raw[i]:0.00} / base {_baseline[i]:0.00})", _style);
        }
      }

      if (GUILayout.Button("Recalibrate", buttonStyle))
      {
        Recalibrate();
      }
      GUILayout.EndArea();
    }
  }
}
