using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public sealed class InstitutionStateDto
    {
        public int SlotCount;
        public List<string> CandidateInstitutionIds = new();
        public List<string> ActiveInstitutionIds = new();
    }
}
