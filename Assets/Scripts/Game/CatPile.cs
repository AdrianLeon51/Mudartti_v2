using System;
using System.Collections.Generic;
using DG.Tweening;
using Mediapipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Mudatti.Game
{
    /// <summary>
    ///   The cats carried in the basket. Collected cats are snapped onto a pile above the starting ones,
    ///   two per layer. Put it on the basket so the pile follows the character's head.
    /// </summary>
    public class CatPile : MonoBehaviour
    {
        [Tooltip("Cats already in the basket; new ones are stacked on top of these.")] [SerializeField]
        private Transform[] startingCats;

        [Tooltip("Height of each new layer, in metres.")] [SerializeField]
        private float layerHeight = 0.2f;

        [Tooltip("Sideways distance of each cat from the pile's centre, in metres.")] [SerializeField]
        private float sideOffset = 0.12f;

        [Tooltip("World scale of the cats in the pile.")] [SerializeField]
        private float catScale = 1.2f;

        [Tooltip("Random turn added to each cat, in degrees, so the pile looks less regular.")] [SerializeField]
        private float yawJitter = 20f;

        private int _added;

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip catFallSound;

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

            Vector3 targetLocalPos = basePosition + new Vector3(side * sideOffset, layer * layerHeight, 0f) / scale;
            Vector3 targetWorldPos = transform.TransformPoint(targetLocalPos);
            cat.SetParent(transform, true);

            CatAnim catAnim = cat.GetComponent<CatAnim>();
            catAnim.ExecuteJumpAnimationForCat(cat, targetWorldPos, baseRotation, scale, catScale, yawJitter);
            RandomCatNoise(true);

            // cat.SetParent(transform, false);
            // cat.localPosition = basePosition + new Vector3(side * sideOffset, layer * layerHeight, 0f) / scale;
            // cat.localRotation = Quaternion.Euler(0f, Random.Range(-yawJitter, yawJitter), 0f) * baseRotation;
            // cat.localScale = Vector3.one * (catScale / scale);
        }

        [SerializeField] GameObject basket;
        [SerializeField] CrouchStops crouchStops;
        [SerializeField] GameObject player;

        [SerializeField] private GameObject finalText;
        [SerializeField] private Text resultTextMesh;

        public void LoseCat()
        {
            _added--;
            crouchStops.fallingCats++;

            if (crouchStops.NoMoreCats)
            {
                float randomSide = Random.value > 0.5f ? 1f : -1f;

                //animation of player falling
                // Define the arc path: 
                // 1. Peak position: Tossed up and sideways (e.g., up by 2 units, sideways by 2 units)
                // 2. Fall position: Plummets down significantly (e.g., world Y = -10 or relative drop)
                Vector3 peakPos = player.transform.position + new Vector3(randomSide * 2f, 2.5f, 0f);
                Vector3 fallPos = player.transform.position + new Vector3(randomSide * 4f, -10f, 0f);

                Vector3[] waypoints = new Vector3[] { peakPos, fallPos };

                // Animate using DOTween Path
                player.transform.DORotate(new Vector3(45f, player.transform.rotation.y, 45f * randomSide), 0.7f);
                player.transform.DOPath(waypoints, 1.2f, PathType.CatmullRom)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => { Destroy(player); });

                GameFlow.Quit(crouchStops, finalText, resultTextMesh);
            }
            else
            {
                GameObject fallingCat = basket.transform.GetChild(basket.transform.childCount - 1).gameObject;

                float randomSide = Random.value > 0.5f ? 1f : -1f;

                // Define the arc path: 
                // 1. Peak position: Tossed up and sideways (e.g., up by 2 units, sideways by 2 units)
                // 2. Fall position: Plummets down significantly (e.g., world Y = -10 or relative drop)
                Vector3 peakPos = fallingCat.transform.position + new Vector3(randomSide * 2f, 2.5f, 0f);
                Vector3 fallPos = fallingCat.transform.position + new Vector3(randomSide * 4f, -10f, 0f);

                Vector3[] waypoints = new Vector3[] { peakPos, fallPos };

                // Animate using DOTween Path
                fallingCat.transform.DOPath(waypoints, 1.2f, PathType.CatmullRom)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => { Destroy(fallingCat); });

                audioSource.PlayOneShot(catFallSound);
            }
        }

        private void Update()
        {
            RandomCatNoise();
        }

        [SerializeField] private List<AudioClip> catSounds;

        public void RandomCatNoise(bool doItAnyway = false)
        {
            if (!doItAnyway)
            {
                bool doIt = Random.Range(0, 1000) == 1;

                if (!doIt)
                    return;
            }

            int random = Random.Range(0, 4);
            audioSource.PlayOneShot(catSounds[random]);
        }
    }
}