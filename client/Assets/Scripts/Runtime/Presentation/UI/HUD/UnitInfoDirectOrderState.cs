namespace Panoptes.Presentation.UI.HUD
{
    public readonly struct UnitInfoDirectOrderState
    {
        public readonly bool CanMove;
        public readonly bool IsMilitaryUnit;
        public readonly bool CanAttack;
        public readonly bool CanCharge;

        public UnitInfoDirectOrderState(bool canMove, bool isMilitaryUnit, bool canAttack, bool canCharge)
        {
            CanMove = canMove;
            IsMilitaryUnit = isMilitaryUnit;
            CanAttack = canAttack;
            CanCharge = canCharge;
        }
    }
}
