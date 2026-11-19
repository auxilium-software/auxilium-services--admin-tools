using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoCaseMessage
    {
        public required string From { get; init; }
        public required string Subject { get; init; }
        public required string Content { get; init; }
        public bool IsUrgent { get; init; }
        public required string SentAt { get; init; }
    }
}
