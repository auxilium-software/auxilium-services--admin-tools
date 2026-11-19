using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoCalendarInvite
    {
        public required string User { get; init; }
        public CalendarEventInviteStatusEnum Status { get; init; } = CalendarEventInviteStatusEnum.Pending;
    }
}
