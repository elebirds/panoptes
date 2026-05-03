using NUnit.Framework;
using Panoptes.Presentation.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class UnitInfoActionButtonBinderTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void ApplyState_ShouldSetVisibilityInteractableAndLabel()
        {
            var button = CreateButton();

            UnitInfoActionButtonBinder.ApplyState(button, "Move", visible: true, interactable: true, actionLocked: false);

            Assert.That(button.gameObject.activeSelf, Is.True);
            Assert.That(button.interactable, Is.True);
            Assert.That(button.GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("Move"));
        }

        [Test]
        public void ApplyState_ShouldDisableInteractable_WhenActionLocked()
        {
            var button = CreateButton();

            UnitInfoActionButtonBinder.ApplyState(button, "Attack", visible: true, interactable: true, actionLocked: true);

            Assert.That(button.gameObject.activeSelf, Is.True);
            Assert.That(button.interactable, Is.False);
        }

        private Button CreateButton()
        {
            _root = new GameObject("ActionButton", typeof(RectTransform), typeof(Button));
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(_root.transform, false);
            return _root.GetComponent<Button>();
        }
    }
}
