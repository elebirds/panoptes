using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public sealed class CatalogAmountDto
    {
        public string Key;
        public int Amount;
    }

    public sealed class CatalogHudEntryDto
    {
        public string Key;
        public string Name;
        public string Description;
        public string IconKey;
        public int SortOrder;
        public bool VisibleInHud;
    }

    public sealed class CatalogRecipeOutputsDto
    {
        public List<CatalogAmountDto> Resources;
        public List<string> Units;
        public List<CatalogAmountDto> PointProgress;
        public List<CatalogAmountDto> StateChanges;
    }

    public sealed class CatalogBuildingDto
    {
        public string Id;
        public string Name;
        public string Description;
        public string IconKey;
        public string PrefabKey;
        public string PlacementKind;
        public string BuildingScope;
        public string RequiredResourceType;
        public string TakeoverMode;
        public int SortOrder;
        public string DefaultRecipeId;
        public List<CatalogAmountDto> ResourceCosts;
        public List<CatalogAmountDto> PointCosts;
        public List<string> RecipeIds;
        public List<string> Tags;
        public int MaxHp;
    }

    public sealed class CatalogRecipeDto
    {
        public string Id;
        public string Name;
        public string Description;
        public string IconKey;
        public string BuildingId;
        public int WorkAmount;
        public int BaseProgress;
        public int SortOrder;
        public List<string> Tags;
        public List<CatalogAmountDto> ResourceInputs;
        public List<CatalogAmountDto> PointInputs;
        public CatalogRecipeOutputsDto Outputs;
    }

    public sealed class CatalogTechnologyPrerequisiteDto
    {
        public string Type;
        public string TargetId;
    }

    public sealed class CatalogTechnologyEffectDto
    {
        public string Type;
        public string TargetId;
        public int InstitutionSlots;
    }

    public sealed class CatalogPolicyModifierEffectDto
    {
        public string Trigger;
        public string TargetId;
        public string ResourceKey;
        public string PointKey;
        public string ModifierType;
        public int Value;
    }

    public sealed class CatalogInstitutionCategoryDto
    {
        public string Id;
        public string Name;
        public string Description;
        public int SortOrder;
        public List<string> Tags;
    }

    public sealed class CatalogInstitutionDto
    {
        public string Id;
        public string Name;
        public string Description;
        public string IconKey;
        public string Category;
        public string ActivationTiming;
        public int SortOrder;
        public List<string> Tags;
        public List<CatalogTechnologyPrerequisiteDto> Prerequisites;
        public List<CatalogTechnologyEffectDto> ExplicitEffects;
        public List<CatalogPolicyModifierEffectDto> ModifierEffects;
    }

    public sealed class CatalogTechnologyDto
    {
        public string Id;
        public string Name;
        public string Description;
        public string IconKey;
        public string Branch;
        public int Tier;
        public int ResearchCost;
        public int SortOrder;
        public List<string> Tags;
        public List<CatalogTechnologyPrerequisiteDto> Prerequisites;
        public List<CatalogTechnologyEffectDto> ExplicitEffects;
    }

    public sealed class CatalogPolicyDto
    {
        public string Id;
        public string Name;
        public string Description;
        public string IconKey;
        public string Layer;
        public string ActivationTiming;
        public List<CatalogPolicyModifierEffectDto> ModifierEffects;
    }

    public sealed class CatalogUnitFlagsDto
    {
        public bool CanAttackStructures;
        public bool CanDestroyRoad;
    }

    public sealed class CatalogUnitDto
    {
        public string Id;
        public string Name;
        public string Description;
        public string IconKey;
        public string PrefabKey;
        public string Class;
        public int MaxHp;
        public int Attack;
        public int AttackRange;
        public int MoveRange;
        public int VisionRange;
        public int RoadSpeedBonus;
        public float ChargeBonus;
        public CatalogUnitFlagsDto Flags;
        public List<string> Tags;
    }

    public sealed class CatalogEmoteSeriesDto
    {
        public string Id;
        public string DisplayName;
        public string IconKey;
        public int SortOrder;
    }

    public sealed class CatalogEmoteDto
    {
        public string Id;
        public string SeriesId;
        public string DisplayName;
        public string AssetKey;
        public int SortOrder;
        public List<string> Tags;
    }

    public sealed class CatalogMapRuntimeNodeDto
    {
        public string Id;
        public int X;
        public int Y;
        public string Terrain;
        public bool HasRoad;
        public bool IsResourcePoint;
        public string ResourceType;
        public string NodeName;
        public string Owner;
        public string TerritoryOwner;
        public string BuildingType;
        public int BuildingHp;
    }

    public sealed class CatalogMapRuntimeBundleDto
    {
        public string Id;
        public string Name;
        public int Width;
        public int Height;
        public List<CatalogMapRuntimeNodeDto> Nodes;
    }
}
