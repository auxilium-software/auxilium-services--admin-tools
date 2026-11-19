using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoDataDefinition
    {
        public string TimeZone { get; init; } = "Europe/London";

        public required string DefaultActor { get; init; }

        public required IReadOnlyList<DemoUser> Users { get; init; }
        public IReadOnlyList<DemoPropertyGroup> CaseAdditionalPropertyGroups { get; init; } = [];
        public IReadOnlyList<DemoCalendarCategory> CalendarEventCategories { get; init; } = [];
        public IReadOnlyList<DemoCase> Cases { get; init; } = [];
        public IReadOnlyList<DemoCalendarEvent> CalendarEvents { get; init; } = [];
        public IReadOnlyList<DemoBulletin> Bulletins { get; init; } = [];
    }
}
