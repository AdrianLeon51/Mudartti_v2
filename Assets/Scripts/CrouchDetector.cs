using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;

public class CrouchDetector : MonoBehaviour
{
    [Header("Thresholds")]
    [Tooltip("Ratio of hip-to-ankle distance relative to shoulder-to-hip distance. Lower values mean crouched.")]
    [SerializeField] private float crouchThreshold = 0.6f;

    /// <summary>
    /// Checks if the pose result indicates a crouch.
    /// </summary>
    public bool IsCrouched(PoseLandmarkerResult result)
    {
        // Check if landmarks were successfully detected
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
        {
            return false;
        }

        var landmarks = result.poseLandmarks[0].landmarks;

        // Extract normalized landmark points (y increases downwards in screen space, 
        // but MediaPipe's normalized landmarks have y from 0 (top) to 1 (bottom))
        var leftHip = landmarks[23];
        var rightHip = landmarks[24];
        var leftKnee = landmarks[25];
        var rightKnee = landmarks[26];
        var leftAnkle = landmarks[27];
        var rightAnkle = landmarks[28];
        var leftShoulder = landmarks[11];
        var rightShoulder = landmarks[12];

        // Ensure visibility/presence confidence if needed (optional check)
        // if (leftHip.Visibility < 0.5f || rightHip.Visibility < 0.5f) return false;

        // Calculate average Y positions (remember: lower Y value is higher up on the screen)
        float avgHipY = (leftHip.y + rightHip.y) / 2f;
        float avgKneeY = (leftKnee.y + rightKnee.y) / 2f;
        float avgAnkleY = (leftAnkle.y + rightAnkle.y) / 2f;
        float avgShoulderY = (leftShoulder.y + rightShoulder.y) / 2f;

        // Compute torso height (Shoulder to Hip) to normalize body proportions 
        // (so distance from camera doesn't break the logic)
        float torsoHeight = Mathf.Abs(avgHipY - avgShoulderY);
        if (torsoHeight <= 0.001f) return false;

        // Compute leg extension or hip height relative to ankles
        // Standing: hips are far above ankles. Crouching: hips approach knee/ankle level.
        float hipToAnkleDistance = Mathf.Abs(avgAnkleY - avgHipY);
        
        // Alternative ratio approach: Ratio of hip height compared to total leg length or torso height
        float crouchRatio = hipToAnkleDistance / torsoHeight;

        // If the distance shrinks below your threshold, they are crouching
        return crouchRatio < crouchThreshold;
    }
}