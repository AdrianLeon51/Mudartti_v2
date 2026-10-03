using Mudatti.Posture;
using UnityEngine;
using UnityEngine.UI;

namespace Mudatti.Interaction
{
  /// <summary>
  ///   On-screen cursor driven by the pose hand points. The cursor only shows while a hand is raised
  ///   above the hips, and clicks a <see cref="HandDwellButton"/> by hovering over it for <see cref="dwellSeconds"/>.
  /// </summary>
  public class HandCursor : MonoBehaviour
  {
    // BlazePose landmark indices
    private const int LeftShoulder = 11, RightShoulder = 12;
    private const int LeftWrist = 15, RightWrist = 16;
    private const int LeftIndex = 19, RightIndex = 20;
    private const int LeftHip = 23, RightHip = 24;
    private const int NoHand = -1, LeftHand = 0, RightHand = 1;

    [SerializeField] private PoseLandmarkFeed feed;
    [SerializeField] private RectTransform cursor;
    [Tooltip("Image with Type = Filled (Radial 360) showing dwell progress.")]
    [SerializeField] private Image dwellRing;

    [Header("Tracking")]
    [SerializeField, Range(0f, 1f)] private float minVisibility = 0.5f;
    [Tooltip("Enable if moving your hand right moves the cursor left.")]
    [SerializeField] private bool mirrorX = false;
    [SerializeField] private float smoothingSeconds = 0.1f;

    [Header("Reach box (in shoulder widths from the shoulder center)")]
    [SerializeField] private float reachHorizontal = 2f;
    [SerializeField] private float reachUp = 1.5f;
    [SerializeField] private float reachDown = 1f;

    [Tooltip("Optional. The reach box maps onto this rectangle instead of the whole screen, so the cursor never leaves it.")]
    [SerializeField] private RectTransform activeArea;

    [Header("Click")]
    [SerializeField] private float dwellSeconds = 1.5f;

    private int _hand = NoHand;
    private bool _hasPosition;
    private Vector2 _screenPosition;
    private Vector2 _targetScreenPosition;
    private HandDwellButton _hovered;
    private HandDwellButton _consumed;
    private float _dwellElapsed;

    public bool IsActive => _hand != NoHand;
    public Vector2 ScreenPosition => _screenPosition;

    private void OnEnable()
    {
      if (feed == null)
      {
        feed = FindFirstObjectByType<PoseLandmarkFeed>();
      }
      if (feed != null)
      {
        feed.FrameUpdated += HandleFrame;
      }
      SetCursorVisible(false);
    }

    private void OnDisable()
    {
      if (feed != null)
      {
        feed.FrameUpdated -= HandleFrame;
      }
    }

    private void HandleFrame(PoseFrame frame)
    {
      _hand = frame.HasPose ? PickHand(frame) : NoHand;
      if (_hand == NoHand)
      {
        _hasPosition = false;
        return;
      }

      var px = frame.Pixel;
      var shoulderMid = (px[LeftShoulder] + px[RightShoulder]) * 0.5f;
      var shoulderWidth = Mathf.Max(1f, Vector2.Distance(px[LeftShoulder], px[RightShoulder]));
      var offset = (HandPoint(frame, _hand) - shoulderMid) / shoulderWidth;

      var x = Mathf.InverseLerp(-reachHorizontal, reachHorizontal, offset.x);
      if (mirrorX)
      {
        x = 1f - x;
      }
      // Image y grows downwards, screen y grows upwards.
      var y = 1f - Mathf.InverseLerp(-reachUp, reachDown, offset.y);
      var area = GetActiveScreenRect();
      _targetScreenPosition = new Vector2(Mathf.Lerp(area.xMin, area.xMax, x), Mathf.Lerp(area.yMin, area.yMax, y));

      if (!_hasPosition)
      {
        _screenPosition = _targetScreenPosition;
        _hasPosition = true;
      }
    }

    private readonly Vector3[] _corners = new Vector3[4];

    private Rect GetActiveScreenRect()
    {
      if (activeArea == null)
      {
        return new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height);
      }
      var canvas = activeArea.GetComponentInParent<Canvas>();
      var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
      activeArea.GetWorldCorners(_corners);
      var min = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
      var max = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);
      return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private int PickHand(PoseFrame frame)
    {
      bool Visible(int i) => frame.IsVisible(i, minVisibility);
      if (!Visible(LeftShoulder) || !Visible(RightShoulder) || !Visible(LeftHip) || !Visible(RightHip))
      {
        return NoHand;
      }

      var hipY = (frame.Pixel[LeftHip].y + frame.Pixel[RightHip].y) * 0.5f;
      var leftRaised = Visible(LeftWrist) && HandPoint(frame, LeftHand).y < hipY;
      var rightRaised = Visible(RightWrist) && HandPoint(frame, RightHand).y < hipY;

      // Keep the current hand while it stays raised; otherwise take the higher one.
      if (_hand == LeftHand && leftRaised) return LeftHand;
      if (_hand == RightHand && rightRaised) return RightHand;
      if (leftRaised && rightRaised)
      {
        return HandPoint(frame, LeftHand).y <= HandPoint(frame, RightHand).y ? LeftHand : RightHand;
      }
      return leftRaised ? LeftHand : rightRaised ? RightHand : NoHand;
    }

    private Vector2 HandPoint(PoseFrame frame, int hand)
    {
      var wrist = hand == LeftHand ? LeftWrist : RightWrist;
      var index = hand == LeftHand ? LeftIndex : RightIndex;
      return frame.IsVisible(index, minVisibility) ? (frame.Pixel[wrist] + frame.Pixel[index]) * 0.5f : frame.Pixel[wrist];
    }

    private void Update()
    {
      if (!_hasPosition)
      {
        SetCursorVisible(false);
        ResetDwell(null);
        _consumed = null;
        return;
      }

      var alpha = smoothingSeconds > 0f ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothingSeconds) : 1f;
      _screenPosition = Vector2.Lerp(_screenPosition, _targetScreenPosition, alpha);
      SetCursorVisible(true);
      MoveCursor(_screenPosition);
      UpdateDwell();
    }

    private void UpdateDwell()
    {
      HandDwellButton hit = null;
      foreach (var target in HandDwellButton.Active)
      {
        if (target.IsClickable && target.Contains(_screenPosition))
        {
          hit = target;
          break;
        }
      }

      // A button that was just clicked needs the cursor to leave before it can fire again.
      if (_consumed != null && hit != _consumed)
      {
        _consumed = null;
      }
      if (hit == null || hit == _consumed)
      {
        ResetDwell(hit);
        return;
      }

      if (hit != _hovered)
      {
        ResetDwell(hit);
      }
      _dwellElapsed += Time.unscaledDeltaTime;
      SetRing(dwellSeconds > 0f ? _dwellElapsed / dwellSeconds : 1f);

      if (_dwellElapsed >= dwellSeconds)
      {
        _consumed = hit;
        ResetDwell(hit);
        hit.Click();
      }
    }

    private void ResetDwell(HandDwellButton hovered)
    {
      _hovered = hovered;
      _dwellElapsed = 0f;
      SetRing(0f);
    }

    private void SetRing(float progress)
    {
      if (dwellRing != null)
      {
        dwellRing.fillAmount = Mathf.Clamp01(progress);
      }
    }

    private void SetCursorVisible(bool visible)
    {
      if (cursor != null && cursor.gameObject.activeSelf != visible)
      {
        cursor.gameObject.SetActive(visible);
      }
    }

    private void MoveCursor(Vector2 screenPoint)
    {
      if (cursor == null)
      {
        return;
      }
      var parent = (RectTransform)cursor.parent;
      var canvas = cursor.GetComponentInParent<Canvas>();
      var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
      if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, cam, out var local))
      {
        cursor.localPosition = local;
      }
    }
  }
}
