using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public sealed class InstitutionStateDto
    {
        public int SlotCount;
        public List<string> CandidatePolicyIds = new();
        public List<string> ActivePolicyIds = new();
    }
}
