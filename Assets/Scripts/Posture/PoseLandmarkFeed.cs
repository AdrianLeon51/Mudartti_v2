using System;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

namespace Mudatti.Posture
{
  /// <summary>
  ///   Latest pose landmarks of a single person. Arrays are reused between frames,
  ///   so consumers must read them during <see cref="PoseLandmarkFeed.FrameUpdated"/> or copy them.
  /// </summary>
  public class PoseFrame
  {
    public const int LandmarkCount = 33;

    public bool HasPose;
    /// <summary>Image landmarks in pixels (origin top-left, y grows downwards).</summary>
    public readonly Vector2[] Pixel = new Vector2[LandmarkCount];
    public readonly float[] Visibility = new float[LandmarkCount];
    /// <summary>World landmarks in meters, hip-centered; z is smaller when closer to the camera.</summary>
    public readonly Vector3[] World = new Vector3[LandmarkCount];

    public bool IsVisible(int index, float minVisibility) => Visibility[index] >= minVisibility;
  }

  /// <summary>
  ///   Receives results from <see cref="PoseLandmarkerRunner"/> (possibly on a background thread)
  ///   and republishes the first pose on the main thread.
  /// </summary>
  public class PoseLandmarkFeed : MonoBehaviour
  {
    [SerializeField] private PoseLandmarkerRunner runner;

    public event Action<PoseFrame> FrameUpdated;
    public PoseFrame Latest { get; } = new PoseFrame();

    private readonly object _lock = new object();
    private readonly Vector3[] _sharedImage = new Vector3[PoseFrame.LandmarkCount]; // x, y normalized; z = visibility
    private readonly Vector3[] _sharedWorld = new Vector3[PoseFrame.LandmarkCount];
    private bool _hasNewFrame;
    private bool _sharedHasPose;

    private bool _subscribed;

    private void OnEnable()
    {
      TrySubscribe();
    }

    private void OnDisable()
    {
      if (_subscribed && runner != null)
      {
        runner.OnPoseResult -= HandlePoseResult;
      }
      _subscribed = false;
    }

    private void TrySubscribe()
    {
      if (runner == null)
      {
        // Lets the feed be dropped into any pose scene without wiring.
        runner = FindFirstObjectByType<PoseLandmarkerRunner>();
      }
      if (runner == null)
      {
        return;
      }
      runner.OnPoseResult += HandlePoseResult;
      _subscribed = true;
      Debug.Log($"PoseLandmarkFeed connected to runner '{runner.name}'", this);
    }

    // May run on a background thread (LIVE_STREAM), so only copy data here.
    private void HandlePoseResult(PoseLandmarkerResult result)
    {
      lock (_lock)
      {
        _hasNewFrame = true;
        _sharedHasPose = result.poseLandmarks != null && result.poseLandmarks.Count > 0
          && result.poseWorldLandmarks != null && result.poseWorldLandmarks.Count > 0
          && result.poseLandmarks[0].landmarks.Count >= PoseFrame.LandmarkCount
          && result.poseWorldLandmarks[0].landmarks.Count >= PoseFrame.LandmarkCount;
        if (!_sharedHasPose)
        {
          return;
        }

        var image = result.poseLandmarks[0].landmarks;
        var world = result.poseWorldLandmarks[0].landmarks;
        for (var i = 0; i < PoseFrame.LandmarkCount; i++)
        {
          _sharedImage[i] = new Vector3(image[i].x, image[i].y, image[i].visibility ?? 1f);
          _sharedWorld[i] = new Vector3(world[i].x, world[i].y, world[i].z);
        }
      }
    }

    private void Update()
    {
      if (!_subscribed)
      {
        TrySubscribe();
      }

      // Normalized image coords are scaled per axis, so convert to pixels before comparing distances.
      var source = ImageSourceProvider.ImageSource;
      var width = source != null && source.textureWidth > 0 ? source.textureWidth : 1f;
      var height = source != null && source.textureHeight > 0 ? source.textureHeight : 1f;

      lock (_lock)
      {
        if (!_hasNewFrame)
        {
          return;
        }
        _hasNewFrame = false;
        Latest.HasPose = _sharedHasPose;
        if (_sharedHasPose)
        {
          for (var i = 0; i < PoseFrame.LandmarkCount; i++)
          {
            Latest.Pixel[i] = new Vector2(_sharedImage[i].x * width, _sharedImage[i].y * height);
            Latest.Visibility[i] = _sharedImage[i].z;
            Latest.World[i] = _sharedWorld[i];
          }
        }
      }

      FrameUpdated?.Invoke(Latest);
    }
  }
}
