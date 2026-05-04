using System.Collections.Generic;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Infrastructure.Mapper
{
    public static class InformationReportMapper
    {
        public static InformationReportDto ToDto(InformationReportView view)
        {
            if (view == null)
            {
                return null;
            }

            return new InformationReportDto
            {
                Mode = view.Mode ?? string.Empty,
                Confidence = view.Confidence ?? string.Empty,
                VisibleNodeCount = view.VisibleNodeCount,
                MemoryNodeCount = view.MemoryNodeCount,
                UnknownNodeCount = view.UnknownNodeCount,
                VisibleUnitCount = view.VisibleUnitCount,
                MemoryUnitCount = view.MemoryUnitCount,
                DelayedCount = view.DelayedCount,
                OmittedCount = view.OmittedCount,
                MisreadCount = view.MisreadCount,
                DirectInspection = view.DirectInspection,
                Notes = view.Notes != null ? new List<string>(view.Notes) : new List<string>()
            };
        }

        public static InformationReportDto Clone(InformationReportDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new InformationReportDto
            {
                Mode = source.Mode ?? string.Empty,
                Confidence = source.Confidence ?? string.Empty,
                VisibleNodeCount = source.VisibleNodeCount,
                MemoryNodeCount = source.MemoryNodeCount,
                UnknownNodeCount = source.UnknownNodeCount,
                VisibleUnitCount = source.VisibleUnitCount,
                MemoryUnitCount = source.MemoryUnitCount,
                DelayedCount = source.DelayedCount,
                OmittedCount = source.OmittedCount,
                MisreadCount = source.MisreadCount,
                DirectInspection = source.DirectInspection,
                Notes = source.Notes != null ? new List<string>(source.Notes) : new List<string>()
            };
        }
    }
}
