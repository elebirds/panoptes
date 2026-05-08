namespace Panoptes.Presentation.UI.HUD
{
    public readonly struct UnitStackOverlayState
    {
        public readonly int Count;
        public readonly string DisplayName;
        public readonly string FallbackText;
        public readonly string IconKey;
        public readonly bool IsMixed;
        public readonly int MaxHp;
        public readonly int Hp;
        public readonly string UnitType;

        public UnitStackOverlayState(
            int count,
            string displayName,
            string fallbackText,
            string iconKey,
            bool isMixed,
            int hp,
            int maxHp,
            string unitType)
        {
            Count = count;
            DisplayName = displayName ?? string.Empty;
            FallbackText = fallbackText ?? string.Empty;
            IconKey = iconKey ?? string.Empty;
            IsMixed = isMixed;
            Hp = hp;
            MaxHp = maxHp <= 0 ? 1 : maxHp;
            UnitType = unitType ?? string.Empty;
        }

        public bool HasUnits => Count > 0 && MaxHp > 0;
    }
}
