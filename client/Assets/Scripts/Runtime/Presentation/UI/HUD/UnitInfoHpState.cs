using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    public readonly struct UnitInfoHpState
    {
        public readonly int Hp;
        public readonly int MaxHp;
        public readonly int ClampedHp;

        public UnitInfoHpState(int hp, int maxHp)
        {
            MaxHp = Mathf.Max(1, maxHp);
            Hp = hp;
            ClampedHp = Mathf.Clamp(hp, 0, MaxHp);
        }

        public string DisplayText => $"{ClampedHp}/{MaxHp}";
    }
}
