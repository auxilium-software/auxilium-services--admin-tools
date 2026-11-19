using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoPropertyGroup
    {
        public required string Ref { get; init; }
        public required string CanonicalName { get; init; }
        public IReadOnlyDictionary<string, string> Translations { get; init; } = new Dictionary<string, string>();
    }
}
