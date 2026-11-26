using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands.Superclasses;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData;
using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Interaction;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Commands;

public sealed class PopulateWithDemoDataCommand(
    AuxiliumDbContext dbContext,
    DemoDataValidator validator,
    DemoDataImporter importer
) : InitialisedInstanceAsyncCommand<PopulateWithDemoDataCommand.Settings>(dbContext)
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-f|--file <PATH>")]
        [Description("Demo data JSON file. Defaults to the demo-data.jsonc shipped alongside the tool.")]
        public string? File { get; set; }
    }

    protected override void WriteHeader()
    {
        AnsiConsole.Write(new Rule("[bold blue]Populate with Demo Data (not easily reversible)[/]").LeftJustified());
        AnsiConsole.WriteLine();
    }

    protected override async Task<int> ExecuteOnInstanceAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        var path = settings.File ?? DemoDataLoader.DefaultPath;
        AnsiConsole.MarkupLineInterpolated($"[grey]Demo data:[/] {path}");

        var (data, loadError) = await DemoDataLoader.LoadAsync(path, cancellationToken);
        if (data is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error: {loadError}[/]");
            return 1;
        }

        var errors = await validator.ValidateAsync(data, cancellationToken);
        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]The demo data has {errors.Count} problem(s); nothing was written:[/]");
            foreach (var error in errors)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]  • {error}[/]");
            }
            return 1;
        }

        var staff = data.Users.Count(u => u.IsStaff);
        var summary = new Table().Border(TableBorder.Rounded).AddColumn("What").AddColumn(new TableColumn("Count").RightAligned());
        summary.AddRow("Users", $"{data.Users.Count} ({staff} staff, {data.Users.Count - staff} clients)");
        summary.AddRow("Case property groups", data.CaseAdditionalPropertyGroups.Count.ToString());
        summary.AddRow("Calendar categories", data.CalendarEventCategories.Count.ToString());
        summary.AddRow("Cases", data.Cases.Count.ToString());
        summary.AddRow("  Messages", data.Cases.Sum(c => c.Messages.Count).ToString());
        summary.AddRow("  Timeline entries", data.Cases.Sum(c => c.TimelineEntries.Count).ToString());
        summary.AddRow("  Todos", data.Cases.Sum(c => c.Todos.Count).ToString());
        summary.AddRow("Calendar events", data.CalendarEvents.Count.ToString());
        summary.AddRow("Bulletins", data.Bulletins.Count.ToString());
        AnsiConsole.Write(summary);

        var confirmation = Prompts.AskConfirmation(
            "This will populate this instance with demo data. This is not easily reversible. Are you sure you want to continue?"
        );

        if (!confirmation)
        {
            AnsiConsole.MarkupLine("[blue]Cancelled, nothing was written.[/]");
            return 0;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Choose one password for every demo account:[/]");
        var password = Prompts.AskNewPassword();
        AnsiConsole.WriteLine();

        DemoDataImportResult result = null!;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Importing demo data…", async status =>
            {
                result = await importer.ImportAsync(
                    data,
                    password,
                    message => status.Status(Markup.Escape(message)),
                    cancellationToken
                );
            });

        AnsiConsole.MarkupLine("[green]✓ Demo data imported.[/]");

        var counts = new Table().Border(TableBorder.Rounded).AddColumn("Created").AddColumn(new TableColumn("Count").RightAligned());
        counts.AddRow("Users", result.Users.Count.ToString());
        counts.AddRow("Case property groups", result.PropertyGroups.ToString());
        counts.AddRow("Calendar categories", result.CalendarCategories.ToString());
        counts.AddRow("Cases", result.Cases.ToString());
        counts.AddRow("  Additional properties", result.CaseProperties.ToString());
        counts.AddRow("  Messages", result.Messages.ToString());
        counts.AddRow("  Timeline entries", result.TimelineEntries.ToString());
        counts.AddRow("  Todos", result.Todos.ToString());
        counts.AddRow("Calendar events", result.CalendarEvents.ToString());
        counts.AddRow("Bulletins", result.Bulletins.ToString());
        AnsiConsole.Write(counts);

        var logins = new Table().Border(TableBorder.Rounded).Title("[bold]Demo logins[/]").AddColumn("Name").AddColumn("Email").AddColumn("Roles");
        foreach (var user in result.Users.OrderByDescending(u => u.IsAdministrator).ThenByDescending(u => u.IsCaseWorkerManager).ThenByDescending(u => u.IsCaseWorker).ThenBy(u => u.FullName))
        {
            var roles = new List<string>();
            if (user.IsAdministrator)
            {
                roles.Add("Administrator");
            }
            if (user.IsCaseWorkerManager)
            {
                roles.Add("Case worker manager");
            }
            if (user.IsCaseWorker)
            {
                roles.Add("Case worker");
            }

            logins.AddRow(
                Markup.Escape(user.FullName),
                Markup.Escape(user.EmailAddress ?? ""),
                roles.Count == 0 ? "[grey]Client[/]" : Markup.Escape(string.Join(", ", roles))
            );
        }
        AnsiConsole.Write(logins);

        return 0;
    }
}
