using UnityEngine;

namespace Mudatti.Game
{
  /// <summary>
  ///   The cats carried in the basket. Collected cats are snapped onto a pile above the starting ones,
  ///   two per layer. Put it on the basket so the pile follows the character's head.
  /// </summary>
  public class CatPile : MonoBehaviour
  {
    [Tooltip("Cats already in the basket; new ones are stacked on top of these.")]
    [SerializeField] private Transform[] startingCats;
    [Tooltip("Height of each new layer, in metres.")]
    [SerializeField] private float layerHeight = 0.2f;
    [Tooltip("Sideways distance of each cat from the pile's centre, in metres.")]
    [SerializeField] private float sideOffset = 0.12f;
    [Tooltip("World scale of the cats in the pile.")]
    [SerializeField] private float catScale = 1.2f;
    [Tooltip("Random turn added to each cat, in degrees, so the pile looks less regular.")]
    [SerializeField] private float yawJitter = 20f;

    private int _added;

    /// <summary>Moves <paramref name="cat" /> into the basket on top of the pile.</summary>
    public void Add(Transform cat)
    {
      var layer = 1 + _added / 2;
      var side = _added % 2 == 0 ? -1f : 1f;
      _added++;

      // The basket is scaled down, so metres are converted to its local units.
      var scale = transform.lossyScale.x;
      var basePosition = Vector3.zero;
      foreach (var start in startingCats)
      {
        basePosition += start.localPosition;
      }
      if (startingCats.Length > 0)
      {
        basePosition /= startingCats.Length;
      }
      var baseRotation = startingCats.Length > 0 ? startingCats[0].localRotation : Quaternion.identity;

      cat.SetParent(transform, false);
      cat.localPosition = basePosition + new Vector3(side * sideOffset, layer * layerHeight, 0f) / scale;
      cat.localRotation = Quaternion.Euler(0f, Random.Range(-yawJitter, yawJitter), 0f) * baseRotation;
      cat.localScale = Vector3.one * (catScale / scale);
    }
  }
}
