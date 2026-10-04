using UnityEngine;
using UnityEngine.Splines;

namespace Mudatti.Game
{
  /// <summary>
  ///   Moves the character along a spline at a constant speed, facing the direction of travel.
  ///   Disable the component to pause.
  /// </summary>
  public class SplineWalker : MonoBehaviour
  {
    [SerializeField] private SplineContainer path;
    [SerializeField] private float speed = 2f;
    [Tooltip("How quickly the speed eases toward a new SpeedMultiplier.")]
    [SerializeField] private float speedChangeSeconds = 0.3f;
    [Tooltip("Keep the character on the terrain surface between knots instead of following the spline's height.")]
    [SerializeField] private bool snapToTerrain;

    private float _currentMultiplier = 1f;

    public SplineContainer Path => path;
    /// <summary>Scales the speed, e.g. lowered while slouching. Changes are eased in.</summary>
    public float SpeedMultiplier { get; set; } = 1f;
    /// <summary>Metres travelled along the path.</summary>
    public float Distance { get; private set; }
    public float Length { get; private set; }
    public bool ReachedEnd => Distance >= Length;

    private void Start()
    {
      Length = path.CalculateLength();
      Place();
    }

    private void Update()
    {
      var alpha = speedChangeSeconds > 0f ? 1f - Mathf.Exp(-Time.deltaTime / speedChangeSeconds) : 1f;
      _currentMultiplier = Mathf.Lerp(_currentMultiplier, SpeedMultiplier, alpha);
      Distance = Mathf.Min(Distance + speed * _currentMultiplier * Time.deltaTime, Length);
      Place();
    }

    private void Place()
    {
      var t = path.Spline.ConvertIndexUnit(Distance, PathIndexUnit.Distance, PathIndexUnit.Normalized);
      Vector3 position = path.EvaluatePosition(t);
      if (snapToTerrain)
      {
        position.y = TerrainHeight(position, position.y);
      }
      transform.position = position;

      // Yaw only, so the character stays upright and its lean pivot still tilts "forward".
      Vector3 forward = path.EvaluateTangent(t);
      forward.y = 0f;
      if (forward.sqrMagnitude > 1e-6f)
      {
        transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
      }
    }

    /// <summary>Height of the terrain tile under <paramref name="position" />, or the fallback off-terrain.</summary>
    public static float TerrainHeight(Vector3 position, float fallback)
    {
      foreach (var terrain in Terrain.activeTerrains)
      {
        var origin = terrain.transform.position;
        var size = terrain.terrainData.size;
        if (position.x >= origin.x && position.x <= origin.x + size.x &&
            position.z >= origin.z && position.z <= origin.z + size.z)
        {
          return terrain.SampleHeight(position) + origin.y;
        }
      }
      return fallback;
    }
  }
}
