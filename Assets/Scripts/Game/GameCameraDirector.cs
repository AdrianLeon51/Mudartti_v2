using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace Mudatti.Game
{
  /// <summary>
  ///   Drives the Cinemachine shots for the walk. Keeps a look-ahead point ahead of the character on the path
  ///   for the walk camera to aim at, and switches to the stop camera while the character is at a stop. The stop
  ///   camera is placed side-on to the character and the cat, so both are in view, and zooms to frame them.
  /// </summary>
  public class GameCameraDirector : MonoBehaviour
  {
    [SerializeField] private SplineWalker walker;
    [SerializeField] private CrouchStops stops;
    [Tooltip("Point the walk camera looks at; placed ahead of the character along the path.")]
    [SerializeField] private Transform lookAhead;
    [Tooltip("How far ahead along the path the walk camera looks, in metres.")]
    [SerializeField] private float lookAheadMetres = 6f;
    [Tooltip("Height of the look-ahead point above the path.")]
    [SerializeField] private float lookAheadHeight = 1.2f;
    [Tooltip("How quickly the look-ahead point follows the path, so curves are eased in.")]
    [SerializeField] private float lookAheadSeconds = 0.5f;
    [Tooltip("Camera that frames the character and the cat at a stop.")]
    [SerializeField] private CinemachineCamera stopCamera;
    [Tooltip("Group the stop camera looks at; its second member is set to the current cat.")]
    [SerializeField] private CinemachineTargetGroup stopGroup;
    [Tooltip("Priority given to the stop camera while stopped; must beat the walk camera's.")]
    [SerializeField] private int stopPriority = 20;
    [SerializeField] private float catRadius = 0.4f;
    [Tooltip("Smallest distance of the stop camera from the midpoint between the character and the cat.")]
    [SerializeField] private float stopMinDistance = 4f;
    [Tooltip("Stop camera distance per metre between the character and the cat.")]
    [SerializeField] private float stopDistancePerMetre = 0.9f;
    [Tooltip("Height of the stop camera above the character's feet.")]
    [SerializeField] private float stopHeight = 1.8f;

    private Transform _framedStop;
    private bool _placed;

    private void Start()
    {
      stopCamera.Priority = 0;
    }

    private void LateUpdate()
    {
      PlaceLookAhead();

      var stop = stops.CurrentStopPoint;
      if (stop == _framedStop)
      {
        return;
      }
      _framedStop = stop;
      if (stop != null)
      {
        // Keep the character as the first member and replace the previous cat.
        while (stopGroup.Targets.Count > 1)
        {
          stopGroup.Targets.RemoveAt(stopGroup.Targets.Count - 1);
        }
        stopGroup.AddMember(stop, 1f, catRadius);
        PlaceStopCamera(stop);
      }
      stopCamera.Priority = stop != null ? stopPriority : 0;
    }

    /// <summary>Puts the stop camera side-on to the character and the cat, on the side the character faces.</summary>
    private void PlaceStopCamera(Transform cat)
    {
      var character = walker.transform.position;
      var toCat = cat.position - character;
      toCat.y = 0f;
      var separation = toCat.magnitude;
      var side = separation > 0.01f ? Vector3.Cross(Vector3.up, toCat / separation) : walker.transform.forward;
      // In front of the character, so its face and crouch are seen rather than its back.
      if (Vector3.Dot(walker.transform.forward, side) < 0f)
      {
        side = -side;
      }
      var midpoint = character + toCat * 0.5f;
      var distance = Mathf.Max(stopMinDistance, separation * stopDistancePerMetre);
      stopCamera.transform.position = midpoint + side * distance + Vector3.up * stopHeight;
      stopCamera.PreviousStateIsValid = false;
    }

    private void PlaceLookAhead()
    {
      var path = walker.Path;
      var length = walker.Length > 0f ? walker.Length : path.CalculateLength();
      var distance = Mathf.Min(walker.Distance + lookAheadMetres, length);
      var t = path.Spline.ConvertIndexUnit(distance, PathIndexUnit.Distance, PathIndexUnit.Normalized);
      Vector3 desired = path.EvaluatePosition(t);
      desired.y += lookAheadHeight;

      var alpha = _placed && lookAheadSeconds > 0f ? 1f - Mathf.Exp(-Time.deltaTime / lookAheadSeconds) : 1f;
      lookAhead.position = Vector3.Lerp(lookAhead.position, desired, alpha);
      _placed = true;
    }
  }
}
