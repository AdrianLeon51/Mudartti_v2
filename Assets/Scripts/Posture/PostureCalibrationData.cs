using System;
using UnityEngine;

namespace Mudatti.Posture
{
  /// <summary>
  ///   The player's calibrated upright posture. Saved in PlayerPrefs so it carries over from the
  ///   Calibration scene to the game scenes (and across app restarts).
  /// </summary>
  [Serializable]
  public class PostureCalibrationData
  {
    private const string PrefsKey = "Mudatti.PostureCalibration";

    public float[] slouchBaseline;
    public float standingLegRatio;
    public string savedAt;

    public static void Save(PostureCalibrationData data)
    {
      data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
      PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
      PlayerPrefs.Save();
    }

    public static bool TryLoad(out PostureCalibrationData data)
    {
      data = null;
      if (!PlayerPrefs.HasKey(PrefsKey))
      {
        return false;
      }
      try
      {
        data = JsonUtility.FromJson<PostureCalibrationData>(PlayerPrefs.GetString(PrefsKey));
      }
      catch (ArgumentException)
      {
        return false;
      }
      return data != null && data.slouchBaseline != null && data.slouchBaseline.Length > 0 && data.standingLegRatio > 0f;
    }

    public static void Clear() => PlayerPrefs.DeleteKey(PrefsKey);
  }
}
