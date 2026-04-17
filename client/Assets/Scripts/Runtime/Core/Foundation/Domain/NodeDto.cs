namespace Panoptes.Core.Domain
{
    public class NodeDto
    {
        public string Id;
        public int X;
        public int Y;
        public string Type;
        public string Owner;
        public string TerritoryOwner;
        public string BuildingType;
        public int BuildingHp;
        public int BuildingMaxHp;
        public string BuildingStatus;
        public string OperationSelectedRecipeId;
        public int OperationCurrentProgress;
        public int OperationRequiredProgress;
        public int OperationBaseProgress;
        public string OperationBlockedReason;
        public string OperationBlockedMessage;
        public string CityId;
        public string ServiceCityId;
        public int TakeoverProgress;
        public int TakeoverRequired;
        public bool IsCityCore;
        public bool IsVisible;
        public bool IsMemory;
        public int LastObservedTurn;
        public bool HasRoad;
        public string Terrain;
        public bool IsResourcePoint;
        public string ResourceType;
        public bool IsSafeZone;
    }
}
