namespace Panoptes.Core.Domain
{
    public sealed class BuildingDto
    {
        public string NodeId;
        public string BuildingTypeId;
        public string OwnerId;
        public string CityId;
        public string ServiceCityId;
        public string Status;
        public int HitPoints;
        public int MaxHitPoints;
        public string OperationSelectedRecipeId;
        public int OperationCurrentProgress;
        public int OperationRequiredProgress;
        public int OperationBaseProgress;
        public string OperationBlockedReason;
        public int TakeoverProgress;
        public int TakeoverRequired;
        public bool IsCityCore;
        public bool IsSafeZone;
    }
}
