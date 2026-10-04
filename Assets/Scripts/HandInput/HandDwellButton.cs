using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mudatti.Interaction
{
  /// <summary>
  ///   Makes a uGUI <see cref="Button"/> clickable by hovering the <see cref="HandCursor"/> over it.
  ///   Regular mouse/touch clicks keep working.
  /// </summary>
  [RequireComponent(typeof(Button))]
  public class HandDwellButton : MonoBehaviour
  {
    private static readonly List<HandDwellButton> _active = new List<HandDwellButton>();
    public static IReadOnlyList<HandDwellButton> Active => _active;

    private Button _button;
    private RectTransform _rectTransform;
    private Canvas _canvas;

    public bool IsClickable => _button != null && _button.isActiveAndEnabled && _button.interactable;

    private void Awake()
    {
      _button = GetComponent<Button>();
      _rectTransform = (RectTransform)transform;
      _canvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable() => _active.Add(this);

    private void OnDisable() => _active.Remove(this);

    public bool Contains(Vector2 screenPoint)
    {
      var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
      return RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, screenPoint, cam);
    }

    public void Click() => _button.onClick.Invoke();
  }
}
