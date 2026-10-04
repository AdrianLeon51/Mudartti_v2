using UnityEngine;
using UnityEngine.UI;

namespace Mudatti.Interaction
{
  /// <summary>Tells the player how to get a hand cursor, and hides once one is showing.</summary>
  [RequireComponent(typeof(Text))]
  public class HandHint : MonoBehaviour
  {
    [SerializeField] private HandCursor handCursor;
    [SerializeField] private string noPersonText = "Step in front of the camera";
    [SerializeField] private string noHandText = "Raise a hand to point at a button";

    private Text _text;

    private void Awake()
    {
      _text = GetComponent<Text>();
      if (handCursor == null)
      {
        handCursor = FindFirstObjectByType<HandCursor>();
      }
    }

    private void Update()
    {
      if (handCursor == null || handCursor.IsActive)
      {
        _text.enabled = false;
        return;
      }
      _text.enabled = true;
      _text.text = handCursor.HasPose ? noHandText : noPersonText;
    }
  }
}
