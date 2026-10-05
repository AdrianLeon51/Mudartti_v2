using System;
using Mudatti.Posture;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UI;

namespace Mudatti.Game
{
  /// <summary>
  ///   Stops the walker at each stop point and runs the tracker's checkpoint sequence there. The character
  ///   idles at the stop, crouches while the player crouches, and walks on once the player has stood back
  ///   up and its stand-up animation has played. The game ends after the last stop.
  /// </summary>
  public class CrouchStops : MonoBehaviour
  {
    private static readonly int StoppedParam = Animator.StringToHash("Stopped");
    private static readonly int CrouchingParam = Animator.StringToHash("Crouching");

    [SerializeField] private PostureTracker tracker;
    [SerializeField] private SplineWalker walker;
    [Tooltip("The character's animator; needs \"Stopped\" and \"Crouching\" bool parameters.")]
    [SerializeField] private Animator animator;
    [Tooltip("Markers placed next to the path; visited in path order.")]
    [SerializeField] private Transform[] stopPoints;
    [Tooltip("Time the stand-up animation needs before the character walks on.")]
    [SerializeField] private float standUpSeconds = 0.93f;
    [Tooltip("Pause after the last stop before the game closes.")]
    [SerializeField] private float finishDelaySeconds = 1f;
    [Tooltip("Cats the player already has; each completed stop adds one.")]
    [SerializeField] private int startingCats = 2;
    [Tooltip("Optional; each stop point is moved into this pile once the crouch has been held.")]
    [SerializeField] private CatPile catPile;

    private float[] _stopDistances;
    private Transform[] _stopOrder;
    private int _nextStop;
    private bool _atStop;
    private bool _standingUp;
    private bool _collected;

    public int fallingCats = 0;
    public int CatsCollected => startingCats + _nextStop + (_collected ? 1 : 0) - fallingCats;
    public int CatsTotal => startingCats + stopPoints.Length;

    public bool NoMoreCats => transform.childCount == 0;
    
    [SerializeField] private GameObject finalText;
    [SerializeField] private Text resultTextMesh;
    
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
      _stopOrder = (Transform[])stopPoints.Clone();
      for (var i = 0; i < stopPoints.Length; i++)
      {
        float3 local = container.transform.InverseTransformPoint(stopPoints[i].position);
        SplineUtility.GetNearestPoint(container.Spline, local, out _, out var t);
        _stopDistances[i] = t * length;
      }
      // Sort the stop points along with their distances, so each stop knows its own point.
      Array.Sort(_stopDistances, _stopOrder);
    }

    [SerializeField] private AudioSource stepAudioSource;
    private void Update()
    {
      if (!_atStop && _nextStop < _stopDistances.Length && walker.Distance >= _stopDistances[_nextStop])
      {
        stepAudioSource.volume = 0;

        _atStop = true;
        _collected = false;
        walker.enabled = false;
        tracker.SetMode(PostureGameMode.Checkpoint);
      }

      // Once the crouch has been held, the stop's cat goes into the basket.
      if (_atStop && !_collected && tracker.CheckpointStep == CheckpointStep.WaitingForStand)
      {
        _collected = true;
        if (catPile != null)
        {
          catPile.Add(_stopOrder[_nextStop]);
        }
      }

      // The character mirrors the player's crouch while at a stop; standing up plays the stand-up animation.
      animator.SetBool(StoppedParam, _atStop || _standingUp || _nextStop >= _stopDistances.Length);
      animator.SetBool(CrouchingParam, _atStop && tracker.IsCrouched);
    }

    private void OnCheckpointCompleted()
    {
      if (!_atStop)
      {
        return;
      }
      _atStop = false;
      _collected = false;
      _nextStop++;

      if (_nextStop >= _stopDistances.Length)
      {
        tracker.SetMode(PostureGameMode.Idle);
        Invoke(nameof(Finish), finishDelaySeconds);
        return;
      }
      // Stay put until the stand-up animation has played, so the character doesn't slide while rising.
      stepAudioSource.volume = 1;
      _standingUp = true;
      Invoke(nameof(WalkOn), standUpSeconds);
    }

    private void WalkOn()
    {
      _standingUp = false;
      tracker.SetMode(PostureGameMode.Running);
      walker.enabled = true;
    }

    private void Finish() => GameFlow.Quit(this, finalText, resultTextMesh);
  }
}
