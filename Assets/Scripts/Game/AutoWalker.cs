using UnityEngine;

namespace Mudatti.Game
{
  /// <summary>Moves the character forward (+Z) at a constant speed.</summary>
  public class AutoWalker : MonoBehaviour
  {
    [SerializeField] private float speed = 2f;

    private void Update()
    {
      transform.position += Vector3.forward * (speed * Time.deltaTime);
    }
  }
}
