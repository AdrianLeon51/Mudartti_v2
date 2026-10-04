using UnityEngine;

namespace Mudatti.Game
{
  /// <summary>
  ///   Endless straight path: child tiles laid out along +Z are moved to the front once the target
  ///   has walked past them.
  /// </summary>
  public class GroundLooper : MonoBehaviour
  {
    [SerializeField] private Transform target;
    [SerializeField] private float tileLength = 10f;

    private void LateUpdate()
    {
      if (target == null || transform.childCount == 0)
      {
        return;
      }
      var span = tileLength * transform.childCount;
      foreach (Transform tile in transform)
      {
        if (target.position.z - tile.position.z > tileLength)
        {
          tile.position += Vector3.forward * span;
        }
      }
    }
  }
}
