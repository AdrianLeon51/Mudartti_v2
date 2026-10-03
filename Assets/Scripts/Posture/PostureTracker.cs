using System;
using UnityEngine;

namespace Mudatti.Posture
{
  public enum PostureGameMode { Idle, Running, Checkpoint }

  public enum CheckpointStep { None, WaitingForCrouch, Holding, WaitingForStand, Completed }

  /// <summary>
  ///   Single entry point for the game. The game sets the mode; this component decides when slouching
  ///   is scored (only while running and standing still upright) and runs the crouch sequence at checkpoints.
  /// </summary>
  public class PostureTracker : MonoBehaviour
  {
    [SerializeField] private SlouchDetector slouchDetector;
    [SerializeField] private CrouchTracker crouchTracker;

    [Tooltip("Restore the calibration saved by the Calibration scene on start.")]
    [SerializeField] private bool loadSavedCalibration = true;

    [Header("Timing")]
    [Tooltip("How long the player must be standing again before slouching is scored.")]
    [SerializeField] private float standSettleSeconds = 1f;
    [SerializeField] private float checkpointHoldSeconds = 0.5f;
    [SerializeField] private float trackingLostSeconds = 1f;

    public event Action SlouchStarted;
    public event Action SlouchEnded;
    public event Action CheckpointCompleted;
    public event Action TrackingLost;
    public event Action TrackingRestored;

    public PostureGameMode Mode { get; private set; } = PostureGameMode.Idle;
    public CheckpointStep CheckpointStep { get; private set; } = CheckpointStep.None;
    public float CheckpointProgress { get; private set; }
    public bool IsTrackingLost { get; private set; }
    public bool IsSlouchActive { get; private set; }
    /// <summary>Why slouching is not being scored right now (empty while it is).</summary>
    public string SlouchPauseReason { get; private set; } = "";
    public bool IsSlouching => IsSlouchActive && slouchDetector.IsSlouching;
    public float SlouchPercent => slouchDetector.SlouchPercent;
    public bool IsCrouched => crouchTracker.IsCrouched;
    public SlouchDetector Slouch => slouchDetector;
    public CrouchTracker Crouch => crouchTracker;

    public string Prompt
    {
      get
      {
        if (IsTrackingLost) return "Step back into view";
        switch (Mode)
        {
          case PostureGameMode.Running:
            return IsSlouching ? "Stand up straight!" : "";
          case PostureGameMode.Checkpoint:
            switch (CheckpointStep)
            {
              case CheckpointStep.WaitingForCrouch: return "Crouch down!";
              case CheckpointStep.Holding: return "Hold it...";
              case CheckpointStep.WaitingForStand: return "Now stand up";
              case CheckpointStep.Completed: return "Well done!";
            }
            break;
        }
        return "";
      }
    }

    private float _holdStart;
    private float _lastValidTime;
    private bool _wasSlouching;

    private void OnEnable()
    {
      slouchDetector.CalibrationStarted += OnCalibrationStarted;
      slouchDetector.Calibrated += OnCalibrated;
    }

    private void OnDisable()
    {
      slouchDetector.CalibrationStarted -= OnCalibrationStarted;
      slouchDetector.Calibrated -= OnCalibrated;
    }

    private void Start()
    {
      _lastValidTime = Time.time;
      if (loadSavedCalibration && PostureCalibrationData.TryLoad(out var data))
      {
        slouchDetector.ApplyBaseline(data.slouchBaseline);
        crouchTracker.SetStandingRatio(data.standingLegRatio);
      }
    }

    public void SetMode(PostureGameMode mode)
    {
      Mode = mode;
      CheckpointStep = mode == PostureGameMode.Checkpoint ? CheckpointStep.WaitingForCrouch : CheckpointStep.None;
      CheckpointProgress = 0f;
    }

    private void OnCalibrationStarted() => crouchTracker.BeginBaseline();

    private void OnCalibrated()
    {
      var standingRatio = crouchTracker.EndBaseline();
      PostureCalibrationData.Save(new PostureCalibrationData
      {
        slouchBaseline = slouchDetector.Baseline,
        standingLegRatio = standingRatio > 0f ? standingRatio : crouchTracker.StandingRatio,
      });
    }

    private void Update()
    {
      var now = Time.time;
      UpdateTracking(now);
      UpdateSlouchGate(now);
      if (Mode == PostureGameMode.Checkpoint)
      {
        UpdateCheckpoint(now);
      }
    }

    private void UpdateTracking(float now)
    {
      if (crouchTracker.State != CrouchState.Unknown)
      {
        _lastValidTime = now;
      }
      var lost = now - _lastValidTime > trackingLostSeconds;
      if (lost == IsTrackingLost)
      {
        return;
      }
      IsTrackingLost = lost;
      (lost ? TrackingLost : TrackingRestored)?.Invoke();
    }

    private void UpdateSlouchGate(float now)
    {
      if (Mode != PostureGameMode.Running) SlouchPauseReason = "not running";
      else if (!slouchDetector.IsCalibrated) SlouchPauseReason = "not calibrated";
      else if (IsTrackingLost) SlouchPauseReason = "tracking lost";
      else if (crouchTracker.IsCrouched) SlouchPauseReason = "crouching";
      else if (!crouchTracker.IsStanding) SlouchPauseReason = "getting up";
      else if (now - crouchTracker.StandingSince < standSettleSeconds) SlouchPauseReason = "settling";
      else SlouchPauseReason = "";

      IsSlouchActive = SlouchPauseReason.Length == 0;
      slouchDetector.Paused = !IsSlouchActive;

      var slouching = IsSlouching;
      if (slouching != _wasSlouching)
      {
        _wasSlouching = slouching;
        (slouching ? SlouchStarted : SlouchEnded)?.Invoke();
      }
    }

    private void UpdateCheckpoint(float now)
    {
      switch (CheckpointStep)
      {
        case CheckpointStep.WaitingForCrouch:
          if (crouchTracker.IsCrouched)
          {
            CheckpointStep = CheckpointStep.Holding;
            _holdStart = now;
          }
          break;
        case CheckpointStep.Holding:
          if (!crouchTracker.IsCrouched)
          {
            CheckpointStep = CheckpointStep.WaitingForCrouch;
            CheckpointProgress = 0f;
            break;
          }
          CheckpointProgress = checkpointHoldSeconds > 0f ? Mathf.Clamp01((now - _holdStart) / checkpointHoldSeconds) : 1f;
          if (CheckpointProgress >= 1f)
          {
            CheckpointStep = CheckpointStep.WaitingForStand;
          }
          break;
        case CheckpointStep.WaitingForStand:
          if (crouchTracker.IsStanding)
          {
            CheckpointStep = CheckpointStep.Completed;
            CheckpointCompleted?.Invoke();
          }
          break;
      }
    }
  }
}
