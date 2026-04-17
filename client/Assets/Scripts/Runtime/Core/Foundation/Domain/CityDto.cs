using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public sealed class CityDto
    {
        public string CityId;
        public string OwnerId;
        public string CoreNodeId;
        public List<string> BuildingNodeIds = new();
    }
}
