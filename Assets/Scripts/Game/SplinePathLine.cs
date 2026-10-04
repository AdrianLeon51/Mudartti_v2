using UnityEngine;
using UnityEngine.Splines;

namespace Mudatti.Game
{
  /// <summary>
  ///   Draws the spline as a flat line on the ground and redraws it while the spline is edited.
  ///   Put it on a child of the spline rotated 90 degrees on X, so the line lies flat.
  /// </summary>
  [ExecuteAlways]
  [RequireComponent(typeof(LineRenderer))]
  public class SplinePathLine : MonoBehaviour
  {
    [SerializeField] private SplineContainer container;
    [SerializeField] private float samplesPerMetre = 2f;
    [Tooltip("Lift above the ground to avoid z-fighting.")]
    [SerializeField] private float heightOffset = 0.02f;
    [Tooltip("Drape the line over the terrain instead of following the spline's height.")]
    [SerializeField] private bool snapToTerrain;

    private LineRenderer _line;

    private void OnEnable()
    {
      if (container == null)
      {
        container = GetComponentInParent<SplineContainer>();
      }
      _line = GetComponent<LineRenderer>();
      Spline.Changed += OnSplineChanged;
      Rebuild();
    }

    private void OnDisable()
    {
      Spline.Changed -= OnSplineChanged;
    }

    private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
    {
      if (container != null && spline == container.Spline)
      {
        Rebuild();
      }
    }

    private void Rebuild()
    {
      if (container == null)
      {
        return;
      }
      var length = container.CalculateLength();
      var count = Mathf.Max(2, Mathf.CeilToInt(length * samplesPerMetre) + 1);
      _line.useWorldSpace = true;
      _line.alignment = LineAlignment.TransformZ;
      _line.positionCount = count;
      for (var i = 0; i < count; i++)
      {
        Vector3 p = container.EvaluatePosition(i / (float)(count - 1));
        if (snapToTerrain)
        {
          p.y = SplineWalker.TerrainHeight(p, p.y);
        }
        _line.SetPosition(i, p + Vector3.up * heightOffset);
      }
    }
  }
}
