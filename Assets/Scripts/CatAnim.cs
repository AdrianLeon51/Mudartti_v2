using System;
using DG.Tweening;
using UnityEngine;

public class CatAnim : MonoBehaviour
{
    [Header("Targets")] [Tooltip("The object this character should look at before jumping.")] [SerializeField]
    private Transform lookAtTarget;

    [Tooltip("The destination Transform point where the object will land.")] [SerializeField]
    private Transform landingPoint;

    [Header("Jump Parameters")] [SerializeField]
    private float duration = 1.0f;

    [SerializeField] private float jumpPower = 2.0f; // Height of the jump arc

    private Vector3 originalScale;

    void Start()
    {
        originalScale = transform.localScale;

        // Wait 2 seconds, then execute the look and jump sequence
        DOVirtual.DelayedCall(5.0f, () =>
        {
            if (lookAtTarget != null)
            {
                transform.LookAt(lookAtTarget, Vector3.up);
            }

            ExecuteJumpAnimation();
        });
    }

    void ExecuteJumpAnimation()
    {
        if (landingPoint == null)
        {
            Debug.LogError("Landing Point is not assigned!", this);
            return;
        }

        Sequence scaleSequence = DOTween.Sequence();

        // Phase 1: Anticipation Squash (Squishing down right before takeoff)
        scaleSequence.Append(transform.DOScale(
            new Vector3(originalScale.x * 1.25f, originalScale.y * 0.7f, originalScale.z * 1.25f),
            duration * 0.15f
        ));

        // Phase 2: Launch Stretch (Stretching vertically as it leaves the ground)
        scaleSequence.Append(transform.DOScale(
            new Vector3(originalScale.x * 0.75f, originalScale.y * 1.35f, originalScale.z * 0.75f),
            duration * 0.2f
        ));

        // Phase 3: Normalize (Return to normal scale during the peak of the arc)
        scaleSequence.Append(transform.DOScale(originalScale, duration * 0.2f));

        // Phase 4: Landing Squash (Flattening out upon impact)
        scaleSequence.Append(transform.DOScale(
            new Vector3(originalScale.x * 1.3f, originalScale.y * 0.6f, originalScale.z * 1.3f),
            duration * 0.15f
        ));

        // Phase 5: Rebound & settle back to original scale
        scaleSequence.Append(transform.DOScale(originalScale, duration * 0.3f));

        // Execute the physical arc movement simultaneously using DOJump
        transform.DOJump(landingPoint.position, jumpPower, 1, duration)
            .SetEase(Ease.OutQuad);

        // Reset X and Z tilt when the jump completes, keeping only the Y rotation (yaw)
        scaleSequence.OnComplete(() => { transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, 0f); });
    }
}