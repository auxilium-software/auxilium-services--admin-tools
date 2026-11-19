using AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Seeding;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData;

public sealed partial class DemoDataValidator(AuxiliumDbContext dbContext)
{
    public static string CalendarCategoryEnumeratorName =>
        TenantSeedDefinition.DataEnumerators
            .Single(e => e.Scope == DataEnumeratorScopeEnum.CalendarEventCategory)
            .CanonicalName;

    public async Task<IReadOnlyList<string>> ValidateAsync(DemoDataDefinition data, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (!RelativeDateTime.IsValidTimeZone(data.TimeZone))
        {
            errors.Add($"TimeZone: '{data.TimeZone}' is not a known time zone.");
            return errors; // every time check below depends on it
        }

        var clock = new RelativeDateTime(DateTime.UtcNow, data.TimeZone);

        void CheckTime(string? value, string where, bool required = true)
        {
            if (value is null)
            {
                if (required)
                {
                    errors.Add($"{where}: a time is required.");
                }
                return;
            }

            if (!clock.TryResolve(value, out _))
            {
                errors.Add($"{where}: '{value}' isn't a valid time (use e.g. -21d, +3d@10:30, or ISO 8601).");
            }
        }

        var users = IndexByRef(data.Users, u => u.Ref, "Users", errors);
        var groups = IndexByRef(data.CaseAdditionalPropertyGroups, g => g.Ref, "CaseAdditionalPropertyGroups", errors);
        var categories = IndexByRef(data.CalendarEventCategories, c => c.Ref, "CalendarEventCategories", errors);
        var cases = IndexByRef(data.Cases, c => c.Ref, "Cases", errors);

        void CheckUser(string? userRef, string where, bool mustBeStaff = false)
        {
            if (userRef is null)
            {
                return;
            }

            if (!users.TryGetValue(userRef, out var user))
            {
                errors.Add($"{where}: no user with Ref '{userRef}'.");
            }
            else if (mustBeStaff && !user.IsStaff)
            {
                errors.Add($"{where}: '{userRef}' must be a staff user (have at least one role).");
            }
        }

        CheckUser(data.DefaultActor, "DefaultActor", mustBeStaff: true);

        foreach (var duplicate in data.Users
            .GroupBy(u => u.EmailAddress.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            errors.Add($"Users: email address '{duplicate.Key}' is used more than once.");
        }

        foreach (var user in data.Users)
        {
            var where = $"Users[{user.Ref}]";

            if (!MailAddress.TryCreate(user.EmailAddress.Trim(), out _))
            {
                errors.Add($"{where}: '{user.EmailAddress}' is not a valid email address.");
            }

            if (!LanguageCodePattern().IsMatch(user.LanguagePreference))
            {
                errors.Add($"{where}: LanguagePreference '{user.LanguagePreference}' should look like en-GB or cy-GB.");
            }

            CheckTime(user.CreatedAt, $"{where}.CreatedAt");
        }

        var emails = data.Users.Select(u => u.EmailAddress.Trim()).ToList();
        var existingEmails = await dbContext.WithinTenancy_Users
            .Where(u => u.EmailAddress != null && emails.Contains(u.EmailAddress))
            .Select(u => u.EmailAddress!)
            .ToListAsync(cancellationToken);

        foreach (var email in existingEmails)
        {
            errors.Add($"Users: '{email}' already exists in this tenant (has the demo data already been imported?).");
        }

        var groupNames = data.CaseAdditionalPropertyGroups.Select(g => g.CanonicalName).ToList();
        foreach (var name in await dbContext.WithinTenancy_CaseAdditionalProperty_Groups
            .Where(g => groupNames.Contains(g.CanonicalName))
            .Select(g => g.CanonicalName)
            .ToListAsync(cancellationToken))
        {
            errors.Add($"CaseAdditionalPropertyGroups: a group named '{name}' already exists in this tenant.");
        }

        if (data.CalendarEventCategories.Count > 0)
        {
            var enumeratorName = CalendarCategoryEnumeratorName;
            var enumerator = await dbContext.WithinTenancy_DataEnumerator_Enumerators
                .Include(e => e.EnumeratorValues)
                .FirstOrDefaultAsync(e => e.CanonicalName == enumeratorName, cancellationToken);

            if (enumerator is null)
            {
                errors.Add($"CalendarEventCategories: this tenant has no '{enumeratorName}' enumerator to add them to.");
            }
            else
            {
                var existingValues = enumerator.EnumeratorValues ?? [];

                foreach (var category in data.CalendarEventCategories
                    .Where(c => existingValues.Any(v => v.CanonicalName == c.CanonicalName)))
                {
                    errors.Add($"CalendarEventCategories[{category.Ref}]: '{category.CanonicalName}' already exists in this tenant.");
                }
            }
        }

        foreach (var category in data.CalendarEventCategories)
        {
            if (!ColourHexPattern().IsMatch(category.ColourHex))
            {
                errors.Add($"CalendarEventCategories[{category.Ref}]: ColourHex '{category.ColourHex}' must look like #2F6F65.");
            }
        }

        foreach (var @case in data.Cases)
        {
            var where = $"Cases[{@case.Ref}]";

            CheckUser(@case.CreatedBy, $"{where}.CreatedBy", mustBeStaff: true);
            CheckTime(@case.CreatedAt, $"{where}.CreatedAt");

            foreach (var client in @case.Clients)
            {
                CheckUser(client, $"{where}.Clients");
            }

            foreach (var worker in @case.Workers)
            {
                CheckUser(worker, $"{where}.Workers", mustBeStaff: true);
            }

            var participants = @case.Clients.Concat(@case.Workers).Append(@case.CreatedBy).ToHashSet();

            foreach (var property in @case.AdditionalProperties)
            {
                if (property.Group is not null && !groups.ContainsKey(property.Group))
                {
                    errors.Add($"{where}.AdditionalProperties[{property.DisplayName}]: no group with Ref '{property.Group}'.");
                }
            }

            foreach (var (message, i) in @case.Messages.Select((m, i) => (m, i)))
            {
                CheckUser(message.From, $"{where}.Messages[{i}].From");
                CheckTime(message.SentAt, $"{where}.Messages[{i}].SentAt");

                if (users.ContainsKey(message.From) && !participants.Contains(message.From))
                {
                    errors.Add($"{where}.Messages[{i}].From: '{message.From}' isn't a client or worker on this case.");
                }
            }

            foreach (var (entry, i) in @case.TimelineEntries.Select((e, i) => (e, i)))
            {
                CheckUser(entry.By, $"{where}.TimelineEntries[{i}].By", mustBeStaff: true);
                CheckTime(entry.OccurredAt, $"{where}.TimelineEntries[{i}].OccurredAt");
            }

            foreach (var (todo, i) in @case.Todos.Select((t, i) => (t, i)))
            {
                CheckUser(todo.By, $"{where}.Todos[{i}].By", mustBeStaff: true);
                CheckUser(todo.AssignedTo, $"{where}.Todos[{i}].AssignedTo", mustBeStaff: true);
                CheckTime(todo.Due, $"{where}.Todos[{i}].Due", required: false);
                CheckTime(todo.CreatedAt, $"{where}.Todos[{i}].CreatedAt", required: false);
            }
        }

        foreach (var (calendarEvent, i) in data.CalendarEvents.Select((e, i) => (e, i)))
        {
            var where = $"CalendarEvents[{i}:{calendarEvent.Title}]";

            CheckUser(calendarEvent.CreatedBy, $"{where}.CreatedBy", mustBeStaff: true);
            CheckTime(calendarEvent.Start, $"{where}.Start");
            CheckTime(calendarEvent.End, $"{where}.End");

            if (clock.TryResolve(calendarEvent.Start, out var start)
                && clock.TryResolve(calendarEvent.End, out var end)
                && end < start)
            {
                errors.Add($"{where}: End is before Start.");
            }

            if (calendarEvent.Category is not null && !categories.ContainsKey(calendarEvent.Category))
            {
                errors.Add($"{where}.Category: no calendar category with Ref '{calendarEvent.Category}'.");
            }

            if (calendarEvent.Case is not null && !cases.ContainsKey(calendarEvent.Case))
            {
                errors.Add($"{where}.Case: no case with Ref '{calendarEvent.Case}'.");
            }

            foreach (var invite in calendarEvent.Invites)
            {
                CheckUser(invite.User, $"{where}.Invites");
            }
        }

        foreach (var (bulletin, i) in data.Bulletins.Select((b, i) => (b, i)))
        {
            var where = $"Bulletins[{i}:{bulletin.Title}]";

            CheckTime(bulletin.StartsAt, $"{where}.StartsAt");
            CheckTime(bulletin.EndsAt, $"{where}.EndsAt", required: false);

            if (bulletin.TargetAudience == SystemBulletinMessageTargetAudienceEnum.SingleUserOnly)
            {
                if (bulletin.SpecificUser is null)
                {
                    errors.Add($"{where}: SpecificUser is required when TargetAudience is SingleUserOnly.");
                }
                else
                {
                    CheckUser(bulletin.SpecificUser, $"{where}.SpecificUser");
                }
            }
            else if (bulletin.SpecificUser is not null)
            {
                errors.Add($"{where}: SpecificUser is only used when TargetAudience is SingleUserOnly.");
            }
        }

        return errors;
    }

    private static Dictionary<string, T> IndexByRef<T>(
        IEnumerable<T> items,
        Func<T, string> getRef,
        string section,
        List<string> errors
    )
    {
        var index = new Dictionary<string, T>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            var reference = getRef(item);

            if (string.IsNullOrWhiteSpace(reference))
            {
                errors.Add($"{section}: an entry has an empty Ref.");
            }
            else if (!index.TryAdd(reference, item))
            {
                errors.Add($"{section}: Ref '{reference}' is used more than once.");
            }
        }

        return index;
    }

    [GeneratedRegex("^[a-z]{2,3}-[A-Z]{2}$")]
    private static partial Regex LanguageCodePattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColourHexPattern();
}
