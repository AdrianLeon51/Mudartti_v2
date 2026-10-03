using Mudatti.Posture;
using UnityEngine;
using UnityEngine.UI;

namespace Mudatti.Calibration
{
  /// <summary>
  ///   Calibration scene flow: wait for a button press, count down, record the upright posture
  ///   with <see cref="SlouchDetector"/>, then show the live slouch percentage.
  /// </summary>
  public class CalibrationFlow : MonoBehaviour
  {
    private enum State { Idle, Countdown, Calibrating, Done }

    [SerializeField] private SlouchDetector slouchDetector;
    [SerializeField] private Button startButton;
    [SerializeField] private Text buttonLabel;
    [SerializeField] private Text instructions;

    [SerializeField] private float countdownSeconds = 2f;

    private State _state;
    private float _countdownEndTime;

    private void OnEnable()
    {
      startButton.onClick.AddListener(OnStartPressed);
      slouchDetector.Calibrated += OnCalibrated;
      EnterIdle();
    }

    private void OnDisable()
    {
      startButton.onClick.RemoveListener(OnStartPressed);
      slouchDetector.Calibrated -= OnCalibrated;
    }

    private void OnStartPressed()
    {
      if (_state != State.Idle && _state != State.Done)
      {
        return;
      }
      _state = State.Countdown;
      _countdownEndTime = Time.time + countdownSeconds;
      startButton.gameObject.SetActive(false);
    }

    private void OnCalibrated()
    {
      if (_state != State.Calibrating)
      {
        return;
      }
      _state = State.Done;
      buttonLabel.text = "Recalibrate";
      startButton.gameObject.SetActive(true);
    }

    private void EnterIdle()
    {
      _state = State.Idle;
      buttonLabel.text = "Start calibration";
      startButton.gameObject.SetActive(true);
    }

    private void Update()
    {
      switch (_state)
      {
        case State.Idle:
          instructions.text = "Raise your hand and hold the cursor over the button";
          break;
        case State.Countdown:
          var remaining = _countdownEndTime - Time.time;
          if (remaining > 0f)
          {
            instructions.text = $"Lower your arms and stand up straight...\n{Mathf.CeilToInt(remaining)}";
          }
          else
          {
            _state = State.Calibrating;
            slouchDetector.StartCalibration();
          }
          break;
        case State.Calibrating:
          instructions.text = slouchDetector.IsCalibrating && slouchDetector.CalibrationProgress > 0f
            ? $"Calibrating... hold still ({slouchDetector.CalibrationProgress * 100f:0}%)"
            : $"Calibrating... {slouchDetector.Status}";
          break;
        case State.Done:
          instructions.text = $"Calibration complete\nSlouch: {slouchDetector.SlouchPercent:0}%";
          break;
      }
    }
  }
}
