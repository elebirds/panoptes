using Panoptes.Presentation.UI.Domestic;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.UI.Turn;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class CameraSafeAreaBootstrapper : MonoBehaviour
    {
        [SerializeField] private float rescanIntervalSeconds = 0.5f;

        private float _nextRescanAt;

        private void Update()
        {
            if (Time.unscaledTime < _nextRescanAt)
            {
                return;
            }

            _nextRescanAt = Time.unscaledTime + Mathf.Max(0.1f, rescanIntervalSeconds);
            EnsureSafeAreaSources();
        }

        private static void EnsureSafeAreaSources()
        {
            EnsureSources<ResourceHUD>();
            EnsureSources<TurnHUD>();
            EnsureSources<UnitInfoPanelController>();
            EnsureSources<BuildCommandPanel>();
            EnsureSources<CityCoreProductionPanel>();
            EnsureSources<RecipeSynthesisPanel>();
            EnsureSources<TechTreePanelController>();
        }

        private static void EnsureSources<T>() where T : Component
        {
            var instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (var i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance == null)
                {
                    continue;
                }

                var rectTransform = instance.transform as RectTransform;
                if (rectTransform == null)
                {
                    continue;
                }

                if (rectTransform.GetComponent<CameraSafeAreaSource>() != null)
                {
                    continue;
                }

                rectTransform.gameObject.AddComponent<CameraSafeAreaSource>();
            }
        }
    }
}
