/*************************************************
 * Project: Panoptes
 * File: UnitMoveAnim.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Unit move animation helper.
 *************************************************/

using System.Collections;
using UnityEngine;
using Panoptes.Presentation.Map;

namespace Panoptes.Presentation.Animation
{
    public sealed class UnitMoveAnim : MonoBehaviour
    {
        public static IEnumerator Play(
            UnitView unitView,
            Vector3 targetWorldPos,
            float duration,
            Camera followCamera,
            bool followCameraEnabled)
        {
            if (unitView == null)
            {
                yield break;
            }

            var startUnitPos = unitView.transform.position;
            var initialDir = targetWorldPos - startUnitPos;
            Vector3 camOffset = Vector3.zero;
            var camStartPos = Vector3.zero;

            if (followCameraEnabled && followCamera != null)
            {
                camStartPos = followCamera.transform.position;
                camOffset = camStartPos - startUnitPos;
            }

            duration = Mathf.Max(0.01f, duration);
            var elapsed = 0f;
            if (unitView != null)
            {
                unitView.SetMovingVisual(true, 1f, initialDir);
            }
            var prevPos = startUnitPos;

            while (elapsed < duration)
            {
                if (unitView == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                // Move unit.
                var unitPos = Vector3.Lerp(startUnitPos, targetWorldPos, t);
                unitView.transform.position = unitPos;
                if (unitView != null)
                {
                    unitView.SetMovingVisual(true, 1f, unitPos - prevPos);
                }
                prevPos = unitPos;

                // Follow camera using the unit's original relative offset.
                if (followCameraEnabled && followCamera != null)
                {
                    followCamera.transform.position = unitPos + camOffset;
                }

                yield return null;
            }

            if (unitView == null)
            {
                yield break;
            }

            unitView.transform.position = targetWorldPos;
            unitView.SetMovingVisual(false, 0f, Vector3.zero);
            if (followCameraEnabled && followCamera != null)
            {
                followCamera.transform.position = targetWorldPos + camOffset;
            }
        }
    }
}
