/*************************************************
 * Project: Panoptes
 * File: DamageNumberPopupController.cs
 * Author: Panoptes Team
 * Date: 2026-04-17
 * Description: World-space floating damage number popup.
 *************************************************/

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class DamageNumberPopupController : MonoBehaviour
    {
        [Header("Popup Motion")]
        [SerializeField] private float popupLifetimeSeconds = 0.78f;
        [SerializeField] private float popupRiseDistance = 1.05f;
        [SerializeField] private float baseWorldOffsetY = 0.36f;
        [SerializeField] private float horizontalJitter = 0.2f;

        [Header("Popup Text")]
        [SerializeField] private float textWorldScale = 0.024f;
        [SerializeField] private float textFontSize = 7.2f;
        [SerializeField] private FontStyles textStyle = FontStyles.Bold;
        [SerializeField] private Color unitDamageColor = new Color(1f, 0.35f, 0.3f, 1f);
        [SerializeField] private Color buildingDamageColor = new Color(1f, 0.55f, 0.2f, 1f);

        private sealed class PopupState
        {
            public Transform Root;
            public TextMeshPro Text;
            public Vector3 StartWorldPosition;
            public Vector3 TravelOffset;
            public float BaseScale;
            public float Elapsed;
            public Color BaseColor;
        }

        private readonly List<PopupState> _active = new List<PopupState>(16);

        public void ShowDamage(Transform target, int damage, bool isBuilding)
        {
            if (target == null || damage <= 0)
            {
                return;
            }

            var popupGo = new GameObject($"DamagePopup_{damage}", typeof(TextMeshPro));
            popupGo.transform.SetParent(transform, worldPositionStays: true);

            var text = popupGo.GetComponent<TextMeshPro>();
            if (text == null)
            {
                Destroy(popupGo);
                return;
            }

            text.text = $"-{damage}";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = Mathf.Max(1f, textFontSize);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.fontStyle = textStyle;
            text.color = isBuilding ? buildingDamageColor : unitDamageColor;

            var start = ResolvePopupStartPosition(target);
            popupGo.transform.position = start;
            popupGo.transform.localScale = Vector3.one * Mathf.Max(0.001f, textWorldScale);

            var jitter = new Vector3(
                Random.Range(-horizontalJitter, horizontalJitter),
                0f,
                Random.Range(-horizontalJitter, horizontalJitter));
            var travel = jitter + Vector3.up * Mathf.Max(0.2f, popupRiseDistance);

            _active.Add(new PopupState
            {
                Root = popupGo.transform,
                Text = text,
                StartWorldPosition = start,
                TravelOffset = travel,
                BaseScale = popupGo.transform.localScale.x,
                Elapsed = 0f,
                BaseColor = text.color
            });

            FaceCamera(popupGo.transform, Camera.main);
        }

        private void LateUpdate()
        {
            if (_active.Count == 0)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            var life = Mathf.Max(0.05f, popupLifetimeSeconds);
            var cam = Camera.main;
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var item = _active[i];
                if (item == null || item.Root == null || item.Text == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                item.Elapsed += dt;
                var t = Mathf.Clamp01(item.Elapsed / life);
                var eased = 1f - (1f - t) * (1f - t);

                item.Root.position = item.StartWorldPosition + item.TravelOffset * eased;
                item.Root.localScale = Vector3.one * (item.BaseScale * ResolveScaleMultiplier(t));

                var color = item.BaseColor;
                color.a = 1f - t;
                item.Text.color = color;

                FaceCamera(item.Root, cam);

                if (t >= 1f)
                {
                    Destroy(item.Root.gameObject);
                    _active.RemoveAt(i);
                }
            }
        }

        private Vector3 ResolvePopupStartPosition(Transform target)
        {
            if (target == null)
            {
                return transform.position;
            }

            var fallback = target.position + Vector3.up * Mathf.Max(0.05f, baseWorldOffsetY);
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return fallback;
            }

            var hasBounds = false;
            var bounds = default(Bounds);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return fallback;
            }

            var yOffset = Mathf.Max(0.05f, baseWorldOffsetY);
            return new Vector3(bounds.center.x, bounds.max.y + yOffset, bounds.center.z);
        }

        private static float ResolveScaleMultiplier(float t)
        {
            var popIn = Mathf.Lerp(0.86f, 1.18f, Mathf.Clamp01(t / 0.18f));
            var settle = Mathf.Lerp(1.18f, 1f, Mathf.Clamp01((t - 0.18f) / 0.5f));
            return t < 0.18f ? popIn : settle;
        }

        private static void FaceCamera(Transform popup, Camera camera)
        {
            if (popup == null || camera == null)
            {
                return;
            }

            var toCamera = camera.transform.position - popup.position;
            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            popup.rotation = Quaternion.LookRotation(toCamera.normalized, camera.transform.up);
        }
    }
}
