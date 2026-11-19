using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoCaseTimelineEntry
    {
        public string? By { get; init; }
        public required string OccurredAt { get; init; }
        public required string Title { get; init; }
        public required string Description { get; init; }
    }
}
