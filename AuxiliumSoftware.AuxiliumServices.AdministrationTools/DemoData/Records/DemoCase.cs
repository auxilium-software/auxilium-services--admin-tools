using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoCase
    {
        public required string Ref { get; init; }
        public required string Title { get; init; }
        public required string Description { get; init; }
        public CaseStatusEnum Status { get; init; } = CaseStatusEnum.Open;
        public CaseSensitivityEnum Sensitivity { get; init; } = CaseSensitivityEnum.Internal;

        public required string CreatedBy { get; init; }
        public required string CreatedAt { get; init; }

        public IReadOnlyList<string> Clients { get; init; } = [];
        public IReadOnlyList<string> Workers { get; init; } = [];
        public IReadOnlyList<DemoCaseProperty> AdditionalProperties { get; init; } = [];
        public IReadOnlyList<DemoCaseMessage> Messages { get; init; } = [];
        public IReadOnlyList<DemoCaseTimelineEntry> TimelineEntries { get; init; } = [];
        public IReadOnlyList<DemoCaseTodo> Todos { get; init; } = [];
    }
}
