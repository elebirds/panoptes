using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public sealed class TechnologyDto
    {
        public string TechnologyId;
        public int CurrentProgress;
        public int RequiredProgress;
        public List<string> CompletedTechnologyIds = new();
        public List<string> ActiveTechnologyIds = new();
        public List<string> PendingActivationTechnologyIds = new();
    }
}
