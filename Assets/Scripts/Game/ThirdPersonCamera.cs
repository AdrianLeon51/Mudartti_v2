using UnityEngine;

namespace Mudatti.Game
{
  /// <summary>Follows the target from behind and above, looking at its upper body.</summary>
  public class ThirdPersonCamera : MonoBehaviour
  {
    [SerializeField] private Transform target;
    [Tooltip("Offset from the target. Slightly to the side so a forward lean is easy to see.")]
    [SerializeField] private Vector3 offset = new Vector3(3f, 2.5f, -5f);
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float followSeconds = 0.15f;

    private void LateUpdate()
    {
      if (target == null)
      {
        return;
      }
      var desired = target.position + offset;
      var alpha = followSeconds > 0f ? 1f - Mathf.Exp(-Time.deltaTime / followSeconds) : 1f;
      transform.position = Vector3.Lerp(transform.position, desired, alpha);
      transform.LookAt(target.position + lookAtOffset);
    }
  }
}
