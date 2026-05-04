using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public sealed class InformationReportDto
    {
        public string Mode;
        public string Confidence;
        public int VisibleNodeCount;
        public int MemoryNodeCount;
        public int UnknownNodeCount;
        public int VisibleUnitCount;
        public int MemoryUnitCount;
        public int DelayedCount;
        public int OmittedCount;
        public int MisreadCount;
        public bool DirectInspection;
        public List<string> Notes;
    }
}
