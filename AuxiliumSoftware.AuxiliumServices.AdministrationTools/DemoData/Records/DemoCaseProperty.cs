using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoCaseProperty
    {
        public string? Group { get; init; }
        public required string DisplayName { get; init; }
        public required string Content { get; init; }
        public string ContentType { get; init; } = "text/plain";
    }
}
