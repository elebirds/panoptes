namespace Panoptes.Core.Domain
{
    public class MinisterMetricDto
    {
        public string Key;
        public float Value;
        public string Label;
    }

    public class MinisterActionItemDto
    {
        public string ActionId;
        public string ActionType;
        public string Description;
        public string TargetNodeId;
        public string TargetUnitId;
    }
}
