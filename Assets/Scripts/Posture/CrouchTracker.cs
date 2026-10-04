using System;
using UnityEngine;

namespace Mudatti.Posture
{
  public enum CrouchState { Unknown, Standing, Transition, Crouched }

  /// <summary>
  ///   Classifies the player as standing, crouched or in between by comparing the hip-to-ankle height
  ///   with the shoulder-to-hip height, relative to the player's own calibrated standing pose.
  ///   Uses hysteresis and hold times so the state doesn't flicker.
  /// </summary>
  public class CrouchTracker : MonoBehaviour
  {
    // BlazePose landmark indices
    private const int LeftShoulder = 11, RightShoulder = 12;
    private const int LeftHip = 23, RightHip = 24;
    private const int LeftKnee = 25, RightKnee = 26;
    private const int LeftAnkle = 27, RightAnkle = 28;
    private static readonly int[] RequiredLandmarks =
      { LeftShoulder, RightShoulder, LeftHip, RightHip, LeftKnee, RightKnee, LeftAnkle, RightAnkle };

    [SerializeField] private PoseLandmarkFeed feed;
    [SerializeField, Range(0f, 1f)] private float minVisibility = 0.5f;

    [Header("Standing reference")]
    [Tooltip("Leg ratio (hip-to-ankle / shoulder-to-hip) used until a calibration provides the player's own.")]
    [SerializeField] private float fallbackStandingRatio = 1.7f;
    [Tooltip("Frames below this absolute leg ratio are ignored while recording the standing baseline.")]
    [SerializeField] private float minBaselineRatio = 1.2f;

    [Header("Thresholds (fraction of the standing ratio)")]
    [SerializeField] private float crouchEnter = 0.65f;
    [SerializeField] private float crouchExit = 0.75f;
    [SerializeField] private float standEnter = 0.85f;
    [SerializeField] private float standExit = 0.8f;

    [Header("Timing")]
    [SerializeField] private float crouchHoldSeconds = 0.15f;
    [SerializeField] private float standHoldSeconds = 0.3f;
    [SerializeField] private float smoothingSeconds = 0.1f;

    private float _standingRatio;
    private float _smoothedRatio = -1f;
    private float _lastSampleTime = -1f;
    private float _belowCrouchSince = -1f;
    private float _aboveStandSince = -1f;

    private bool _recordingBaseline;
    private float _baselineSum;
    private int _baselineCount;

    public event Action Crouched;
    public event Action StoodUp;

    public CrouchState State { get; private set; } = CrouchState.Unknown;
    public bool IsCrouched => State == CrouchState.Crouched;
    public bool IsStanding => State == CrouchState.Standing;
    /// <summary>Time.time when the current Standing state began.</summary>
    public float StandingSince { get; private set; }
    /// <summary>Current leg ratio divided by the standing ratio: ~1 standing, lower when crouching.</summary>
    public float NormalizedHeight { get; private set; }
    public float StandingRatio => _standingRatio;
    public bool HasCalibratedRatio { get; private set; }

    private void Awake()
    {
      _standingRatio = fallbackStandingRatio;
    }

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

    public void SetStandingRatio(float ratio)
    {
      if (ratio > 0f)
      {
        _standingRatio = ratio;
        HasCalibratedRatio = true;
      }
    }

    public void BeginBaseline()
    {
      _recordingBaseline = true;
      _baselineSum = 0f;
      _baselineCount = 0;
    }

    /// <summary>Stops recording and applies the averaged standing ratio. Returns it, or 0 if nothing was recorded.</summary>
    public float EndBaseline()
    {
      _recordingBaseline = false;
      if (_baselineCount == 0)
      {
        return 0f;
      }
      var ratio = _baselineSum / _baselineCount;
      SetStandingRatio(ratio);
      return ratio;
    }

    private void HandleFrame(PoseFrame frame)
    {
      var now = Time.time;
      var dt = _lastSampleTime < 0f ? 0f : now - _lastSampleTime;
      _lastSampleTime = now;

      if (!TryGetLegRatio(frame, out var ratio))
      {
        SetState(CrouchState.Unknown, now);
        _smoothedRatio = -1f;
        _belowCrouchSince = _aboveStandSince = -1f;
        return;
      }

      if (_recordingBaseline && ratio >= minBaselineRatio)
      {
        _baselineSum += ratio;
        _baselineCount++;
      }

      var alpha = _smoothedRatio < 0f || smoothingSeconds <= 0f ? 1f : 1f - Mathf.Exp(-dt / smoothingSeconds);
      _smoothedRatio = _smoothedRatio < 0f ? ratio : Mathf.Lerp(_smoothedRatio, ratio, alpha);
      NormalizedHeight = _smoothedRatio / _standingRatio;

      _belowCrouchSince = NormalizedHeight < crouchEnter ? (_belowCrouchSince < 0f ? now : _belowCrouchSince) : -1f;
      _aboveStandSince = NormalizedHeight > standEnter ? (_aboveStandSince < 0f ? now : _aboveStandSince) : -1f;

      switch (State)
      {
        case CrouchState.Standing:
          if (NormalizedHeight < standExit) SetState(CrouchState.Transition, now);
          break;
        case CrouchState.Crouched:
          if (NormalizedHeight > crouchExit) SetState(CrouchState.Transition, now);
          break;
        default:
          if (_belowCrouchSince >= 0f && now - _belowCrouchSince >= crouchHoldSeconds) SetState(CrouchState.Crouched, now);
          else if (_aboveStandSince >= 0f && now - _aboveStandSince >= standHoldSeconds) SetState(CrouchState.Standing, now);
          else if (State == CrouchState.Unknown) SetState(CrouchState.Transition, now);
          break;
      }
    }

    private bool TryGetLegRatio(PoseFrame frame, out float ratio)
    {
      ratio = 0f;
      if (!frame.HasPose)
      {
        return false;
      }
      foreach (var i in RequiredLandmarks)
      {
        if (!frame.IsVisible(i, minVisibility))
        {
          return false;
        }
      }

      var px = frame.Pixel;
      var shoulderY = (px[LeftShoulder].y + px[RightShoulder].y) * 0.5f;
      var hipY = (px[LeftHip].y + px[RightHip].y) * 0.5f;
      var ankleY = (px[LeftAnkle].y + px[RightAnkle].y) * 0.5f;
      var torso = hipY - shoulderY; // image y grows downwards
      if (torso < 1f)
      {
        return false;
      }
      ratio = Mathf.Max(0f, ankleY - hipY) / torso;
      return true;
    }

    private void SetState(CrouchState state, float now)
    {
      if (State == state)
      {
        return;
      }
      State = state;
      if (state == CrouchState.Standing)
      {
        StandingSince = now;
        StoodUp?.Invoke();
      }
      else if (state == CrouchState.Crouched)
      {
        Crouched?.Invoke();
      }
    }
  }
}
