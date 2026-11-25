using AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Enums;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Services;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.EntityModels;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using AuxiliumSoftware.AuxiliumServices.Common.Enumerators;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData;

public sealed record DemoDataImportResult(
    IReadOnlyList<UserEntityModel> Users,
    int PropertyGroups,
    int CalendarCategories,
    int Cases,
    int CaseProperties,
    int Messages,
    int TimelineEntries,
    int Todos,
    int CalendarEvents,
    int Bulletins
);

public sealed class DemoDataImporter(
    AuxiliumDbContext dbContext,
    AdministratorAccountService accountService,
    ICaseDocumentService caseDocuments,
    IMessageDocumentService messageDocuments,
    ICaseAdditionalPropertyGroupService propertyGroups,
    IDataEnumeratorService dataEnumerators
)
{
    private sealed record AuditKey(Guid CaseId, CaseEntityTypeEnum EntityType, Guid? EntityId, AuditLogActionTypeEnum Action);

    public async Task<DemoDataImportResult> ImportAsync(
        DemoDataDefinition data,
        string rawPassword,
        Action<string> reportProgress,
        CancellationToken cancellationToken
    )
    {
        var clock = new RelativeDateTime(DateTime.UtcNow, data.TimeZone);

        var users = new Dictionary<string, UserEntityModel>();
        var groups = new Dictionary<string, Guid>();
        var categories = new Dictionary<string, Guid>();
        var cases = new Dictionary<string, CaseEntityModel>();

        var backdates = new List<Action>();
        var auditTimes = new Dictionary<AuditKey, DateTime>();

        var counts = new Dictionary<string, int>();
        void Count(string what) => counts[what] = counts.GetValueOrDefault(what) + 1;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var (demoUser, i) in data.Users.Select((u, i) => (u, i)))
        {
            reportProgress($"Creating users ({i + 1}/{data.Users.Count})…");

            users[demoUser.Ref] = await accountService.CreateUserAsync(
                new UserAccountDetails
                {
                    EmailAddress = demoUser.EmailAddress,
                    FullName = demoUser.FullName,
                    LanguagePreference = demoUser.LanguagePreference,
                    /*
                    TelephoneNumber = demoUser.TelephoneNumber,
                    FullAddress = demoUser.FullAddress,
                    Gender = demoUser.Gender,
                    DateOfBirth = demoUser.DateOfBirth,
                    HowDidYouFindOutAboutOurService = demoUser.HowDidYouFindOutAboutOurService,
                    */
                    IsAdministrator = demoUser.Roles.Contains(DemoUserRoleEnum.Administrator),
                    IsCaseWorker = demoUser.Roles.Contains(DemoUserRoleEnum.CaseWorker),
                    IsCaseWorkerManager = demoUser.Roles.Contains(DemoUserRoleEnum.CaseWorkerManager),
                    CreatedAtUtc = clock.Resolve(demoUser.CreatedAt),
                },
                rawPassword,
                cancellationToken
            );
        }

        var defaultActor = users[data.DefaultActor];

        reportProgress("Creating case property groups…");

        foreach (var group in data.CaseAdditionalPropertyGroups)
        {
            var created = await propertyGroups.CreateGroupAsync(
                defaultActor.Id,
                group.CanonicalName,
                group.Translations,
                cancellationToken
            );

            groups[group.Ref] = created.Id;
            Count(nameof(DemoDataImportResult.PropertyGroups));
        }

        if (data.CalendarEventCategories.Count > 0)
        {
            reportProgress("Creating calendar categories…");

            var enumerator = await dataEnumerators.GetEnumeratorByNameAsync(
                DemoDataValidator.CalendarCategoryEnumeratorName,
                ct: cancellationToken
            ) ?? throw new InvalidOperationException("The calendar event category enumerator disappeared mid-import.");

            foreach (var category in data.CalendarEventCategories)
            {
                var value = await dataEnumerators.CreateValueAsync(
                    enumerator.Id,
                    category.CanonicalName,
                    category.ColourHex,
                    defaultActor,
                    ct: cancellationToken
                );

                foreach (var (languageCode, translation) in category.Translations)
                {
                    await dataEnumerators.CreateTranslationAsync(value.Id, languageCode, translation, defaultActor, cancellationToken);
                }

                categories[category.Ref] = value.Id;
                Count(nameof(DemoDataImportResult.CalendarCategories));
            }
        }

        foreach (var (demoCase, i) in data.Cases.Select((c, i) => (c, i)))
        {
            reportProgress($"Creating cases ({i + 1}/{data.Cases.Count}): {demoCase.Title}…");

            var owner = users[demoCase.CreatedBy];
            var caseCreatedAt = clock.Resolve(demoCase.CreatedAt);
            var latestActivity = caseCreatedAt;

            var caseEntity = new CaseEntityModel
            {
                Id = UUIDUtilities.GenerateV5(DatabaseObjectTypeEnum.WithinTenancy_Case),
                CreatedAtUtc = caseCreatedAt,
                CreatedByUserId = owner.Id,
                Title = demoCase.Title,
                Description = demoCase.Description,
                Status = demoCase.Status,
                Sensitivity = demoCase.Sensitivity,
            };

            dbContext.WithinTenancy_Cases.Add(caseEntity);
            await dbContext.SaveChangesAsync(cancellationToken);
            await caseDocuments.WriteToAuditLog(owner.Id, caseEntity.Id, AuditLogActionTypeEnum.Creation, CaseEntityTypeEnum.Case, caseEntity.Id, ct: cancellationToken);
            auditTimes[new AuditKey(caseEntity.Id, CaseEntityTypeEnum.Case, caseEntity.Id, AuditLogActionTypeEnum.Creation)] = caseCreatedAt;

            cases[demoCase.Ref] = caseEntity;
            Count(nameof(DemoDataImportResult.Cases));

            foreach (var clientRef in demoCase.Clients)
            {
                await caseDocuments.AddClientAsync(caseEntity.Id, users[clientRef].Id, owner.Id);
                auditTimes[new AuditKey(caseEntity.Id, CaseEntityTypeEnum.Case_Client, users[clientRef].Id, AuditLogActionTypeEnum.Assignment)] = caseCreatedAt;
            }

            foreach (var workerRef in demoCase.Workers)
            {
                await caseDocuments.AddWorkerAsync(caseEntity.Id, users[workerRef].Id, owner.Id);
                auditTimes[new AuditKey(caseEntity.Id, CaseEntityTypeEnum.Case_Worker, users[workerRef].Id, AuditLogActionTypeEnum.Assignment)] = caseCreatedAt;
            }

            var caseId = caseEntity.Id;
            backdates.Add(() =>
            {
                foreach (var entry in dbContext.ChangeTracker.Entries<CaseClientEntityModel>().Where(e => e.Entity.CaseId == caseId))
                {
                    entry.Entity.CreatedAtUtc = caseCreatedAt;
                }

                foreach (var entry in dbContext.ChangeTracker.Entries<CaseWorkerEntityModel>().Where(e => e.Entity.CaseId == caseId))
                {
                    entry.Entity.CreatedAtUtc = caseCreatedAt;
                }
            });

            foreach (var property in demoCase.AdditionalProperties)
            {
                var propertyId = await caseDocuments.SaveAdditionalPropertyAsync(
                    owner,
                    caseEntity.Id,
                    property.Group is null ? null : groups[property.Group],
                    property.DisplayName,
                    property.Content,
                    property.ContentType
                );

                auditTimes[new AuditKey(caseEntity.Id, CaseEntityTypeEnum.Case_AdditionalProperty, propertyId, AuditLogActionTypeEnum.Creation)] = caseCreatedAt;
                backdates.Add(() =>
                {
                    var entity = dbContext.WithinTenancy_CaseAdditionalProperties.Local.Single(p => p.Id == propertyId);
                    entity.CreatedAtUtc = caseCreatedAt;
                });
                Count(nameof(DemoDataImportResult.CaseProperties));
            }

            foreach (var demoMessage in demoCase.Messages)
            {
                var sentAt = clock.Resolve(demoMessage.SentAt);
                var message = await messageDocuments.CreateMessageAsync(
                    caseEntity.Id,
                    demoMessage.Subject,
                    demoMessage.Content,
                    users[demoMessage.From].Id,
                    demoMessage.IsUrgent
                );

                backdates.Add(() =>
                {
                    message.CreatedAtUtc = sentAt;
                    message.LastUpdatedAtUtc = sentAt;
                });
                latestActivity = Max(latestActivity, sentAt);
                Count(nameof(DemoDataImportResult.Messages));
            }

            foreach (var demoEntry in demoCase.TimelineEntries)
            {
                var author = users[demoEntry.By ?? demoCase.CreatedBy];
                var occurredAt = clock.Resolve(demoEntry.OccurredAt);

                var entry = await caseDocuments.CreateTimelineEntryAsync(
                    caseEntity.Id,
                    occurredAt,
                    demoEntry.Title,
                    demoEntry.Description,
                    author.Id
                );

                auditTimes[new AuditKey(caseEntity.Id, CaseEntityTypeEnum.Case_TimelineEntry, entry.Id, AuditLogActionTypeEnum.Creation)] = occurredAt;
                backdates.Add(() => entry.CreatedAtUtc = occurredAt);
                latestActivity = Max(latestActivity, occurredAt);
                Count(nameof(DemoDataImportResult.TimelineEntries));
            }

            foreach (var demoTodo in demoCase.Todos)
            {
                var author = users[demoTodo.By ?? demoCase.CreatedBy];
                var assignee = demoTodo.AssignedTo is null ? null : users[demoTodo.AssignedTo];
                var todoCreatedAt = clock.ResolveOptional(demoTodo.CreatedAt) ?? caseCreatedAt;

                var todo = await caseDocuments.CreateTodoAsync(
                    caseEntity.Id,
                    demoTodo.Summary,
                    demoTodo.Description,
                    demoTodo.Priority,
                    author.Id,
                    dueDate: clock.ResolveOptional(demoTodo.Due),
                    assignedTo: assignee?.Id
                );

                auditTimes[new AuditKey(caseEntity.Id, CaseEntityTypeEnum.Case_Todo, todo.Id, AuditLogActionTypeEnum.Creation)] = todoCreatedAt;

                if (demoTodo.Status != TodoStatusEnum.NeedsAction)
                {
                    var completer = assignee ?? author;
                    await caseDocuments.UpdateTodoStatusAsync(
                        caseEntity.Id,
                        todo.Id,
                        completer.Id,
                        demoTodo.Status,
                        completedBy: demoTodo.Status == TodoStatusEnum.Completed ? completer.Id : null,
                        completionNotes: demoTodo.CompletionNote
                    );
                }

                var changedAt = todoCreatedAt + (clock.NowUtc - todoCreatedAt) / 2;
                auditTimes[new AuditKey(caseEntity.Id, CaseEntityTypeEnum.Case_Todo, todo.Id, AuditLogActionTypeEnum.Modification)] = changedAt;

                backdates.Add(() =>
                {
                    todo.CreatedAtUtc = todoCreatedAt;
                    if (todo.LastUpdatedAtUtc is not null)
                    {
                        todo.LastUpdatedAtUtc = changedAt;
                    }
                    if (todo.CompletedAtUtc is not null)
                    {
                        todo.CompletedAtUtc = changedAt;
                    }
                });
                latestActivity = Max(latestActivity, demoTodo.Status == TodoStatusEnum.NeedsAction ? todoCreatedAt : changedAt);
                Count(nameof(DemoDataImportResult.Todos));
            }

            var caseLastUpdatedAt = Max(latestActivity, caseCreatedAt);
            backdates.Add(() =>
            {
                caseEntity.LastUpdatedAtUtc = caseLastUpdatedAt;
                caseEntity.LastUpdatedByUserId ??= owner.Id;
            });
        }

        reportProgress("Creating calendar events…");

        foreach (var demoEvent in data.CalendarEvents)
        {
            var organiser = users[demoEvent.CreatedBy];
            var start = clock.Resolve(demoEvent.Start);
            var createdAt = Min(start, clock.NowUtc).AddDays(-7);

            var calendarEvent = new CalendarEventEntityModel
            {
                Id = UUIDUtilities.GenerateV5(DatabaseObjectTypeEnum.WithinTenancy_Calendar_Event),
                CreatedAtUtc = createdAt,
                CreatedByUserId = organiser.Id,
                Title = demoEvent.Title,
                Description = demoEvent.Description,
                Location = demoEvent.Location,
                CategoryValueId = demoEvent.Category is null ? null : categories[demoEvent.Category],
                CaseId = demoEvent.Case is null ? null : cases[demoEvent.Case].Id,
                StartUtc = start,
                EndUtc = clock.Resolve(demoEvent.End),
                IsAllDay = demoEvent.IsAllDay,
            };

            dbContext.WithinTenancy_CalendarEvents.Add(calendarEvent);

            foreach (var invite in demoEvent.Invites)
            {
                dbContext.WithinTenancy_CalendarEventInvites.Add(new CalendarEventInviteEntityModel
                {
                    Id = UUIDUtilities.GenerateV5(DatabaseObjectTypeEnum.WithinTenancy_Calendar_EventInvite),
                    CreatedAtUtc = createdAt,
                    CreatedByUserId = organiser.Id,
                    CalendarEventId = calendarEvent.Id,
                    InvitedUserId = users[invite.User].Id,
                    InvitedByUserId = organiser.Id,
                    InvitedAtUtc = createdAt,
                    Status = invite.Status,
                    RespondedAtUtc = invite.Status == CalendarEventInviteStatusEnum.Pending ? null : createdAt.AddDays(1),
                });
            }

            Count(nameof(DemoDataImportResult.CalendarEvents));
        }

        foreach (var demoBulletin in data.Bulletins)
        {
            var startsAt = clock.Resolve(demoBulletin.StartsAt);

            dbContext.WithinTenancy_System_Bulletins.Add(new SystemBulletinEntryEntityModel
            {
                Id = UUIDUtilities.GenerateV5(DatabaseObjectTypeEnum.WithinTenancy_System_BulletinEntry),
                CreatedAtUtc = Min(startsAt, clock.NowUtc),
                CreatedByUserId = defaultActor.Id,
                Title = demoBulletin.Title,
                Content = demoBulletin.Content,
                Severity = demoBulletin.Severity,
                TargetAudience = demoBulletin.TargetAudience,
                SpecificUserId = demoBulletin.SpecificUser is null ? null : users[demoBulletin.SpecificUser].Id,
                IsActive = true,
                IsDismissible = demoBulletin.IsDismissible,
                StartsAtUtc = startsAt,
                EndsAtUtc = clock.ResolveOptional(demoBulletin.EndsAt),
            });

            Count(nameof(DemoDataImportResult.Bulletins));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        reportProgress("Back-dating records…");

        foreach (var backdate in backdates)
        {
            backdate();
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<LogCaseModificationEventEntityModel>())
        {
            var log = entry.Entity;
            if (auditTimes.TryGetValue(new AuditKey(log.CaseId, log.EntityType, log.EntityId, log.Action), out var when))
            {
                log.CreatedAtUtc = when;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DemoDataImportResult(
            Users: users.Values.ToList(),
            PropertyGroups: counts.GetValueOrDefault(nameof(DemoDataImportResult.PropertyGroups)),
            CalendarCategories: counts.GetValueOrDefault(nameof(DemoDataImportResult.CalendarCategories)),
            Cases: counts.GetValueOrDefault(nameof(DemoDataImportResult.Cases)),
            CaseProperties: counts.GetValueOrDefault(nameof(DemoDataImportResult.CaseProperties)),
            Messages: counts.GetValueOrDefault(nameof(DemoDataImportResult.Messages)),
            TimelineEntries: counts.GetValueOrDefault(nameof(DemoDataImportResult.TimelineEntries)),
            Todos: counts.GetValueOrDefault(nameof(DemoDataImportResult.Todos)),
            CalendarEvents: counts.GetValueOrDefault(nameof(DemoDataImportResult.CalendarEvents)),
            Bulletins: counts.GetValueOrDefault(nameof(DemoDataImportResult.Bulletins))
        );
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
