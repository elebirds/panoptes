using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    /// <summary>
    /// Base class for unit-info action injectors.
    /// Subclasses register unit-specific actions into UnitInfoActionRegistry.
    /// </summary>
    public abstract class UnitInfoActionProviderBase : MonoBehaviour
    {
        [SerializeField] protected UnitInfoActionRegistry actionRegistry;

        private bool _registered;

        protected virtual void Awake()
        {
            TryRegister();
        }

        protected virtual void OnEnable()
        {
            TryRegister();
        }

        public void EnsureRegistered()
        {
            TryRegister();
        }

        public void EnsureRegistered(UnitInfoActionRegistry registry)
        {
            if (actionRegistry == null)
            {
                actionRegistry = registry;
            }

            TryRegister();
        }

        protected void TryRegister()
        {
            if (_registered)
            {
                return;
            }

            if (actionRegistry == null)
            {
                actionRegistry = GetComponent<UnitInfoActionRegistry>();
            }

            if (actionRegistry == null)
            {
                return;
            }

            RegisterActions(actionRegistry);
            _registered = true;
        }

        protected abstract void RegisterActions(UnitInfoActionRegistry registry);
    }
}
