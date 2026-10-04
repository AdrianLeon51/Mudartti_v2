using System;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

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
    }

    public void ExecuteJumpAnimationForCat(Transform cat, Vector3 landingWorldPos, Quaternion baseRotation, float basketScale, float catScale, float yawJitter)
    {
        if (lookAtTarget != null) { cat.LookAt(lookAtTarget, Vector3.up); }

        Vector3 originalScale = cat.localScale;
        Vector3 finalTargetScale = Vector3.one * (catScale / basketScale);

        Sequence scaleSequence = DOTween.Sequence();

        // Phase 1: Anticipation Squash (Squishing down right before takeoff)
        scaleSequence.Append(cat.DOScale(
            new Vector3(originalScale.x * 1.25f, originalScale.y * 0.7f, originalScale.z * 1.25f),
            duration * 0.15f
        ));

        // Phase 2: Launch Stretch (Stretching vertically as it leaves the ground)
        scaleSequence.Append(cat.DOScale(
            new Vector3(originalScale.x * 0.75f, originalScale.y * 1.35f, originalScale.z * 0.75f),
            duration * 0.2f
        ));

        // Phase 3: Normalize scale towards the basket's target cat scale during peak arc
        scaleSequence.Append(cat.DOScale(finalTargetScale, duration * 0.2f));

        // Phase 4: Landing Squash (Flattening out upon impact)
        scaleSequence.Append(cat.DOScale(
            new Vector3(finalTargetScale.x * 1.3f, finalTargetScale.y * 0.6f, finalTargetScale.z * 1.3f),
            duration * 0.15f
        ));

        // Phase 5: Rebound & settle back to final target scale
        scaleSequence.Append(cat.DOScale(finalTargetScale, duration * 0.3f));

        // Execute the physical arc movement simultaneously using DOJump to the calculated basket slot
        cat.DOJump(landingWorldPos, jumpPower, 1, duration)
            .SetEase(Ease.OutQuad);

        // When the jump completes, apply the final local rotation (with yaw jitter) and ensure clean final scale/rotation
        float randomYaw = Random.Range(-yawJitter, yawJitter);
        Quaternion finalLocalRot = baseRotation * Quaternion.Euler(0f, randomYaw, 0f);

        scaleSequence.OnComplete(() =>
        {
            cat.localRotation = finalLocalRot;
            cat.localScale = finalTargetScale;
        });
    }

    // public void ExecuteJumpAnimation()
    // {
    //     if (lookAtTarget != null)
    //     {
    //         transform.LookAt(lookAtTarget, Vector3.up);
    //     }
    //     
    //     if (landingPoint == null)
    //     {
    //         Debug.LogError("Landing Point is not assigned!", this);
    //         return;
    //     }
    //
    //     Sequence scaleSequence = DOTween.Sequence();
    //
    //     // Phase 1: Anticipation Squash (Squishing down right before takeoff)
    //     scaleSequence.Append(transform.DOScale(
    //         new Vector3(originalScale.x * 1.25f, originalScale.y * 0.7f, originalScale.z * 1.25f),
    //         duration * 0.15f
    //     ));
    //
    //     // Phase 2: Launch Stretch (Stretching vertically as it leaves the ground)
    //     scaleSequence.Append(transform.DOScale(
    //         new Vector3(originalScale.x * 0.75f, originalScale.y * 1.35f, originalScale.z * 0.75f),
    //         duration * 0.2f
    //     ));
    //
    //     // Phase 3: Normalize (Return to normal scale during the peak of the arc)
    //     scaleSequence.Append(transform.DOScale(originalScale, duration * 0.2f));
    //
    //     // Phase 4: Landing Squash (Flattening out upon impact)
    //     scaleSequence.Append(transform.DOScale(
    //         new Vector3(originalScale.x * 1.3f, originalScale.y * 0.6f, originalScale.z * 1.3f),
    //         duration * 0.15f
    //     ));
    //
    //     // Phase 5: Rebound & settle back to original scale
    //     scaleSequence.Append(transform.DOScale(originalScale, duration * 0.3f));
    //
    //     // Execute the physical arc movement simultaneously using DOJump
    //     transform.DOJump(landingPoint.position, jumpPower, 1, duration)
    //         .SetEase(Ease.OutQuad);
    //
    //     // Reset X and Z tilt when the jump completes, keeping only the Y rotation (yaw)
    //     scaleSequence.OnComplete(() => { transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, 0f); });
    // }
}