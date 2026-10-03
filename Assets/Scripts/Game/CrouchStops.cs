using System;
using Mudatti.Posture;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Mudatti.Game
{
  /// <summary>
  ///   Stops the walker at each stop point and runs the tracker's checkpoint sequence there. The character
  ///   shrinks once the crouch has been held long enough and walks on after the player stands back up.
  ///   The game ends after the last stop.
  /// </summary>
  public class CrouchStops : MonoBehaviour
  {
    [SerializeField] private PostureTracker tracker;
    [SerializeField] private SplineWalker walker;
    [Tooltip("Scaled on Y; place it at the character's feet so it shrinks toward the ground.")]
    [SerializeField] private Transform shrinkPivot;
    [Tooltip("Markers placed next to the path; visited in path order.")]
    [SerializeField] private Transform[] stopPoints;
    [SerializeField, Range(0.1f, 1f)] private float crouchedHeightScale = 0.5f;
    [Tooltip("Pause after the last stop before the game closes.")]
    [SerializeField] private float finishDelaySeconds = 1f;
    [Tooltip("Cats the player already has; each completed stop adds one.")]
    [SerializeField] private int startingCats = 2;

    private float[] _stopDistances;
    private int _nextStop;
    private bool _atStop;

    public int CatsCollected => startingCats + _nextStop;
    public int CatsTotal => startingCats + stopPoints.Length;

    private void OnEnable()
    {
      tracker.CheckpointCompleted += OnCheckpointCompleted;
    }

    private void OnDisable()
    {
      tracker.CheckpointCompleted -= OnCheckpointCompleted;
    }

    private void Start()
    {
      // Project each marker onto the path to get its distance along it.
      var container = walker.Path;
      var length = container.CalculateLength();
      _stopDistances = new float[stopPoints.Length];
      for (var i = 0; i < stopPoints.Length; i++)
      {
        float3 local = container.transform.InverseTransformPoint(stopPoints[i].position);
        SplineUtility.GetNearestPoint(container.Spline, local, out _, out var t);
        _stopDistances[i] = t * length;
      }
      Array.Sort(_stopDistances);
    }

    private void Update()
    {
      if (!_atStop && _nextStop < _stopDistances.Length && walker.Distance >= _stopDistances[_nextStop])
      {
        _atStop = true;
        walker.enabled = false;
        tracker.SetMode(PostureGameMode.Checkpoint);
      }

      // Shrink only after the crouch has been held for the tracker's checkpoint hold time.
      var step = tracker.CheckpointStep;
      var shrunk = _atStop && (step == CheckpointStep.WaitingForStand || step == CheckpointStep.Completed);
      SetHeightScale(shrunk ? crouchedHeightScale : 1f);
    }

    private void OnCheckpointCompleted()
    {
      if (!_atStop)
      {
        return;
      }
      _atStop = false;
      _nextStop++;
      SetHeightScale(1f);

      if (_nextStop >= _stopDistances.Length)
      {
        tracker.SetMode(PostureGameMode.Idle);
        Invoke(nameof(Finish), finishDelaySeconds);
        return;
      }
      tracker.SetMode(PostureGameMode.Running);
      walker.enabled = true;
    }

    private void Finish() => GameFlow.Quit();

    private void SetHeightScale(float y)
    {
      var scale = shrinkPivot.localScale;
      shrinkPivot.localScale = new Vector3(scale.x, y, scale.z);
    }
  }
}
