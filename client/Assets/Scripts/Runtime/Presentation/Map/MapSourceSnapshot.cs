using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Presentation.Map
{
    internal readonly struct MapSourceSnapshot
    {
        public MapSourceSnapshot(List<NodeDto> nodes, List<UnitDto> units, string mapId)
        {
            Nodes = nodes ?? new List<NodeDto>();
            Units = units ?? new List<UnitDto>();
            MapId = mapId ?? string.Empty;
        }

        public List<NodeDto> Nodes { get; }
        public List<UnitDto> Units { get; }
        public string MapId { get; }
        public bool HasNodes => Nodes != null && Nodes.Count > 0;
    }
}
