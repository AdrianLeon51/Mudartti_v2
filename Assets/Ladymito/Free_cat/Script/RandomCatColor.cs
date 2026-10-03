using UnityEngine;

namespace Ladymito.Free_cat.Script
{
    public class RandomCatColor : MonoBehaviour
    {
        void Start()
        {
            Renderer rend = GetComponentInChildren<Renderer>();

            if (rend != null)
            {
                Color randomColor = Random.ColorHSV(0f, 1f, 0f, 1f, 1f, 1f, 1f, 1f);
                Material mat = rend.material;
                mat.SetColor("_BaseColor", randomColor);
            }
        }
    }
}