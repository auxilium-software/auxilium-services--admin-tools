using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Common;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.EntityModels;
using AuxiliumSoftware.AuxiliumServices.Common.Enumerators;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Services.Implementations;
using AuxiliumSoftware.AuxiliumServices.Common.Utilities;
using Konscious.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Security.Cryptography;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Tools;

public sealed class CreateAdminUserTool(
    IConfiguration configuration,
    IPasswordService passwordService
) : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold blue]===== Auxilium Admin User Creator =====[/]");
        AnsiConsole.WriteLine();

        using var dbContext = MariaDBInteractions.GetDbContext(configuration);

        var email = AnsiConsole.Ask<string>("[green]Email:[/]");

        var rawPassword = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Password:[/]")
                .PromptStyle("red")
                .Secret());

        var fullName = AnsiConsole.Ask<string>("[green]Full Name:[/]");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(rawPassword) || string.IsNullOrWhiteSpace(fullName))
        {
            AnsiConsole.MarkupLine("[red]Error: All fields are required.[/]");
            return 1;
        }

        if (await dbContext.Users.AnyAsync(u => u.EmailAddress == email, cancellationToken))
        {
            AnsiConsole.MarkupLine($"[red]Error: A user with email '{email}' already exists.[/]");
            return 1;
        }

        var userId = UUIDUtilities.GenerateV5(DatabaseObjectTypeEnum.User);

        var normalisedPassword = passwordService.NormalisePassword(rawPassword, null);
        var hashedPassword = passwordService.HashPassword(normalisedPassword);

        var user = new UserEntityModel
        {
            Id = userId,
            EmailAddress = email,
            PasswordHash = hashedPassword,
            FullName = fullName,
            FullAddress = "",
            TelephoneNumber = "",
            Gender = "",
            DateOfBirth = new DateOnly(),
            HowDidYouFindOutAboutOurService = "",
            LanguagePreference = "en-GB",
            IsAdministrator = true,
            IsCaseWorker = false,
            IsCaseWorkerManager = false,
            AllowLogin = true,
            MustChangePassword = false,
            HasEmailAddressBeenVerified = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = null,
            LastUpdatedAtUtc = null,
            LastUpdatedByUserId = null,
            DeletionRequested = false,
            DeletionRequestReason = null,
        };

        var userWhitelist = new SystemWafUserWhitelistEntryEntityModel
        {
            Id = userId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            UserId = userId,
            JustificationForWhitelist = "Admin user created via admin tool",
            IsPermanent = true,
        };

        dbContext.Users.Add(user);
        dbContext.System_Waf_UserWhitelist.Add(userWhitelist);

        await dbContext.SaveChangesAsync(cancellationToken);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Admin user created successfully![/]");

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Field");
        table.AddColumn("Value");
        table.AddRow("ID", userId.ToString());
        table.AddRow("Email", email);
        table.AddRow("Name", fullName);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]You can now log in with these credentials.[/]");

        return 0;
    }
}
