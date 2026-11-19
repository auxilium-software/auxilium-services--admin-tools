using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoBulletin
    {
        public required string Title { get; init; }
        public required string Content { get; init; }
        public SystemBulletinMessageSeverityEnum Severity { get; init; } = SystemBulletinMessageSeverityEnum.Informational;
        public SystemBulletinMessageTargetAudienceEnum TargetAudience { get; init; } = SystemBulletinMessageTargetAudienceEnum.Everyone;

        public string? SpecificUser { get; init; }

        public bool IsDismissible { get; init; } = true;
        public string StartsAt { get; init; } = "0d";
        public string? EndsAt { get; init; }
    }
}
