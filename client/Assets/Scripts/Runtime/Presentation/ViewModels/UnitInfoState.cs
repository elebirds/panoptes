namespace Panoptes.Presentation.ViewModels
{
    public sealed class UnitInfoState
    {
        public UnitInfoState(
            bool hasSelection = false,
            string unitId = "",
            string unitType = "",
            string ownerId = "",
            string displayName = "单位",
            string description = "",
            int hp = 0,
            int maxHp = 1,
            string planningSummary = "",
            bool showDirectOrderButtons = false,
            bool canMove = false,
            bool isMilitaryUnit = false,
            bool canAttack = false,
            bool canCharge = false,
            bool actionLocked = false)
        {
            ActionLocked = actionLocked;
            CanAttack = canAttack;
            CanCharge = canCharge;
            CanMove = canMove;
            Description = description ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "单位" : displayName;
            HasSelection = hasSelection;
            Hp = hp;
            IsMilitaryUnit = isMilitaryUnit;
            MaxHp = maxHp <= 0 ? 1 : maxHp;
            OwnerId = ownerId ?? string.Empty;
            PlanningSummary = planningSummary ?? string.Empty;
            ShowDirectOrderButtons = showDirectOrderButtons;
            UnitId = unitId ?? string.Empty;
            UnitType = unitType ?? string.Empty;
        }

        public bool ActionLocked { get; }
        public bool CanAttack { get; }
        public bool CanCharge { get; }
        public bool CanMove { get; }
        public string Description { get; }
        public bool DescriptionVisible => !string.IsNullOrWhiteSpace(Description);
        public string DisplayName { get; }
        public bool HasSelection { get; }
        public int Hp { get; }
        public bool IsMilitaryUnit { get; }
        public int MaxHp { get; }
        public string OwnerId { get; }
        public string PlanningSummary { get; }
        public bool PlanningSummaryVisible => !string.IsNullOrWhiteSpace(PlanningSummary);
        public bool ShowDirectOrderButtons { get; }
        public string UnitId { get; }
        public string UnitType { get; }

        public UnitInfoState WithActionLocked(bool actionLocked)
        {
            return new UnitInfoState(
                HasSelection,
                UnitId,
                UnitType,
                OwnerId,
                DisplayName,
                Description,
                Hp,
                MaxHp,
                PlanningSummary,
                ShowDirectOrderButtons,
                CanMove,
                IsMilitaryUnit,
                CanAttack,
                CanCharge,
                actionLocked);
        }
    }
}
