/*************************************************
 * Project: Panoptes
 * File: UnitMoveAnim.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Unit move animation helper.
 *************************************************/

using System.Collections;
using Panoptes.Presentation.Map;
using UnityEngine;

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
            _ = followCamera;
            _ = followCameraEnabled;

            if (unitView == null)
            {
                yield break;
            }

            var startUnitPos = unitView.transform.position;
            var initialDir = targetWorldPos - startUnitPos;

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

                yield return null;
            }

            if (unitView == null)
            {
                yield break;
            }

            unitView.transform.position = targetWorldPos;
            unitView.SetMovingVisual(false, 0f, Vector3.zero);
        }
    }
}
