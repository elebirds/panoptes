using System;
using Panoptes.Presentation.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.HUD
{
    public readonly struct UnitInfoDirectOrderButtonActions
    {
        public readonly Action Move;
        public readonly Action Attack;
        public readonly Action Hold;
        public readonly Action Charge;

        public UnitInfoDirectOrderButtonActions(Action move, Action attack, Action hold, Action charge)
        {
            Move = move;
            Attack = attack;
            Hold = hold;
            Charge = charge;
        }

        public static UnitInfoDirectOrderButtonActions ForController(
            Func<MapPlanningInputController> resolveController)
        {
            MapPlanningInputController Resolve()
            {
                return resolveController != null ? resolveController() : null;
            }

            return new UnitInfoDirectOrderButtonActions(
                () => Resolve()?.BeginMoveSelection(),
                () => Resolve()?.BeginAttackSelection(),
                () => Resolve()?.IssueHoldOrder(),
                () => Resolve()?.BeginChargeSelection());
        }
    }

    public readonly struct UnitInfoDirectOrderButtons
    {
        public readonly Button Move;
        public readonly Button Attack;
        public readonly Button Hold;
        public readonly Button Charge;

        public UnitInfoDirectOrderButtons(Button move, Button attack, Button hold, Button charge)
        {
            Move = move;
            Attack = attack;
            Hold = hold;
            Charge = charge;
        }
    }

    public sealed class UnitInfoDirectOrderPanelBinder
    {
        public UnitInfoDirectOrderButtons EnsureButtons(
            RectTransform root,
            Button moveButton,
            Button attackButton,
            Button holdButton,
            Button chargeButton,
            Color defaultButtonColor)
        {
            if (root == null)
            {
                return new UnitInfoDirectOrderButtons(moveButton, attackButton, holdButton, chargeButton);
            }

            return new UnitInfoDirectOrderButtons(
                moveButton ?? CreateButton(root, "MoveButton", "移动", new Vector2(0f, 0f), new Vector2(88f, 30f), defaultButtonColor),
                attackButton ?? CreateButton(root, "AttackButton", "攻击", new Vector2(98f, 0f), new Vector2(88f, 30f), defaultButtonColor),
                holdButton ?? CreateButton(root, "HoldButton", "待命", new Vector2(0f, -38f), new Vector2(88f, 30f), defaultButtonColor),
                chargeButton ?? CreateButton(root, "ChargeButton", "冲锋", new Vector2(98f, -38f), new Vector2(88f, 30f), defaultButtonColor));
        }

        public Button CreateButton(
            RectTransform root,
            string objectName,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            Color defaultButtonColor)
        {
            if (root == null)
            {
                return null;
            }

            var buttonRect = UnitInfoPanelLayoutBuilder.EnsureRect(root, objectName);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = size;

            var image = buttonRect.GetComponent<Image>();
            if (image == null)
            {
                image = buttonRect.gameObject.AddComponent<Image>();
            }

            image.color = defaultButtonColor;
            if (image.sprite == null)
            {
                image.sprite = UnitInfoActionListBinder.GetFallbackButtonSprite();
            }

            var button = buttonRect.GetComponent<Button>();
            if (button == null)
            {
                button = buttonRect.gameObject.AddComponent<Button>();
            }

            var labelRect = buttonRect.Find("Label") as RectTransform;
            if (labelRect == null)
            {
                labelRect = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
                labelRect.SetParent(buttonRect, false);
            }

            UnitInfoPanelLayoutBuilder.StretchToParent(labelRect, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            var labelText = UnitInfoPanelLayoutBuilder.CreateTmpText(labelRect, label);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 15f;
            return button;
        }

        public void BindListeners(
            UnitInfoDirectOrderButtons buttons,
            UnitInfoDirectOrderButtonActions actions)
        {
            Bind(buttons.Move, actions.Move);
            Bind(buttons.Attack, actions.Attack);
            Bind(buttons.Hold, actions.Hold);
            Bind(buttons.Charge, actions.Charge);
        }

        public void ApplyState(
            RectTransform root,
            UnitInfoDirectOrderButtons buttons,
            bool visible,
            UnitInfoDirectOrderState state,
            bool actionLocked)
        {
            if (root != null)
            {
                root.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                ApplyButtonState(buttons.Move, "Move", visible: false, interactable: false, actionLocked: actionLocked);
                ApplyButtonState(buttons.Attack, "Attack", visible: false, interactable: false, actionLocked: actionLocked);
                ApplyButtonState(buttons.Hold, "Hold", visible: false, interactable: false, actionLocked: actionLocked);
                ApplyButtonState(buttons.Charge, "Charge", visible: false, interactable: false, actionLocked: actionLocked);
                return;
            }

            ApplyButtonState(buttons.Move, "Move", visible: true, interactable: state.CanMove, actionLocked: actionLocked);
            ApplyButtonState(buttons.Attack, "Attack", visible: state.IsMilitaryUnit, interactable: state.CanAttack, actionLocked: actionLocked);
            ApplyButtonState(buttons.Hold, "Hold", visible: state.IsMilitaryUnit, interactable: state.IsMilitaryUnit, actionLocked: actionLocked);
            ApplyButtonState(buttons.Charge, "Charge", visible: state.IsMilitaryUnit, interactable: state.CanCharge, actionLocked: actionLocked);
        }

        private static void Bind(Button button, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(() => action());
            }
        }

        private static void ApplyButtonState(
            Button button,
            string label,
            bool visible,
            bool interactable,
            bool actionLocked)
        {
            UnitInfoActionButtonBinder.ApplyState(button, label, visible, interactable, actionLocked);
        }
    }
}
