using AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoUser
    {
        public required string Ref { get; init; }
        public required string FullName { get; init; }
        public required string EmailAddress { get; init; }

        public IReadOnlyList<DemoUserRoleEnum> Roles { get; init; } = [];

        public string LanguagePreference { get; init; } = "en-GB";
        public string? TelephoneNumber { get; init; }
        public string? FullAddress { get; init; }
        public string? Gender { get; init; }
        public DateOnly? DateOfBirth { get; init; }
        public string? HowDidYouFindOutAboutOurService { get; init; }
        public string CreatedAt { get; init; } = "0d";

        public bool IsStaff => Roles.Count > 0;
    }
}
