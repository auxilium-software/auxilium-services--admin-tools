using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoCalendarEvent
    {
        public required string Title { get; init; }
        public string? Description { get; init; }
        public string? Location { get; init; }

        public string? Category { get; init; }

        public string? Case { get; init; }

        public required string CreatedBy { get; init; }
        public required string Start { get; init; }
        public required string End { get; init; }
        public bool IsAllDay { get; init; }
        public IReadOnlyList<DemoCalendarInvite> Invites { get; init; } = [];
    }
}
