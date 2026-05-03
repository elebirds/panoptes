using System;
using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public class ResourceDto
    {
        public int Ore;
        public int Wood;
        public int Food;
        public int IndustryOutput;
        public Dictionary<string, int> ResourceAmounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> PointAmounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
