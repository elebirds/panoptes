using NUnit.Framework;
using Panoptes.Presentation.Binders.Ugui;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class UnitInfoUguiBinderTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void Render_ShouldApplyUnitInfoStateToUguiControls()
        {
            _root = new GameObject("UnitInfoBinderTestRoot", typeof(RectTransform));
            var nameText = CreateText("Name");
            var descriptionText = CreateText("Description");
            var planningText = CreateText("Planning");
            var hpSlider = CreateChild("HpSlider").AddComponent<Slider>();
            var hpText = CreateText("Hp");
            var directRoot = CreateChild("DirectRoot").GetComponent<RectTransform>();
            var move = CreateButton("Move");
            var attack = CreateButton("Attack");
            var hold = CreateButton("Hold");
            var charge = CreateButton("Charge");
            var binder = new UnitInfoUguiBinder(
                new UnitInfoUguiBinder.References(
                    nameText,
                    descriptionText,
                    planningText,
                    hpSlider,
                    hpText,
                    directRoot,
                    new UnitInfoDirectOrderButtons(move, attack, hold, charge)),
                new UnitInfoDirectOrderPanelBinder());

            binder.Render(new UnitInfoState(
                hasSelection: true,
                unitId: "u1",
                unitType: "fighter",
                displayName: "Fighter",
                description: "Frontline unit",
                hp: 8,
                maxHp: 10,
                planningSummary: "Planned: move -> n2",
                showDirectOrderButtons: true,
                canMove: true,
                isMilitaryUnit: true,
                canAttack: true,
                canCharge: false,
                actionLocked: false));

            Assert.That(nameText.text, Is.EqualTo("Fighter"));
            Assert.That(descriptionText.text, Is.EqualTo("Frontline unit"));
            Assert.That(descriptionText.gameObject.activeSelf, Is.True);
            Assert.That(planningText.text, Is.EqualTo("Planned: move -> n2"));
            Assert.That(planningText.gameObject.activeSelf, Is.True);
            Assert.That(hpSlider.maxValue, Is.EqualTo(10f));
            Assert.That(hpSlider.value, Is.EqualTo(8f));
            Assert.That(hpText.text, Is.EqualTo("8/10"));
            Assert.That(directRoot.gameObject.activeSelf, Is.True);
            Assert.That(move.gameObject.activeSelf, Is.True);
            Assert.That(move.interactable, Is.True);
            Assert.That(attack.gameObject.activeSelf, Is.True);
            Assert.That(attack.interactable, Is.True);
            Assert.That(hold.gameObject.activeSelf, Is.True);
            Assert.That(hold.interactable, Is.True);
            Assert.That(charge.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Render_ShouldClearControlsWhenNoUnitSelected()
        {
            _root = new GameObject("UnitInfoBinderTestRoot", typeof(RectTransform));
            var nameText = CreateText("Name");
            var descriptionText = CreateText("Description");
            var planningText = CreateText("Planning");
            var hpSlider = CreateChild("HpSlider").AddComponent<Slider>();
            var hpText = CreateText("Hp");
            var directRoot = CreateChild("DirectRoot").GetComponent<RectTransform>();
            var binder = new UnitInfoUguiBinder(
                new UnitInfoUguiBinder.References(
                    nameText,
                    descriptionText,
                    planningText,
                    hpSlider,
                    hpText,
                    directRoot,
                    default),
                new UnitInfoDirectOrderPanelBinder());

            binder.Render(new UnitInfoState());

            Assert.That(nameText.text, Is.Empty);
            Assert.That(descriptionText.gameObject.activeSelf, Is.False);
            Assert.That(planningText.gameObject.activeSelf, Is.False);
            Assert.That(hpSlider.maxValue, Is.EqualTo(1f));
            Assert.That(hpSlider.value, Is.EqualTo(0f));
            Assert.That(hpText.text, Is.EqualTo("0/1"));
            Assert.That(directRoot.gameObject.activeSelf, Is.False);
        }

        private TMP_Text CreateText(string name)
        {
            return CreateChild(name).AddComponent<TextMeshProUGUI>();
        }

        private Button CreateButton(string name)
        {
            var buttonObject = CreateChild(name);
            buttonObject.AddComponent<Image>();
            return buttonObject.AddComponent<Button>();
        }

        private GameObject CreateChild(string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(_root.transform, false);
            return child;
        }
    }
}
