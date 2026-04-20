using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Common;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Security.Cryptography;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Tools
{
    public class PasswordResetTool(IConfiguration configuration, IPasswordService passwordService) : AsyncCommand
    {
        public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            AnsiConsole.MarkupLine("[bold blue]===== Auxilium Password Reset Tool =====[/]");
            AnsiConsole.WriteLine();

            using var dbContext = MariaDBInteractions.GetDbContext(configuration);

            var email = AnsiConsole.Ask<string>("[green]User Email:[/]");

            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.EmailAddress == email, cancellationToken);

            if (user == null)
            {
                AnsiConsole.MarkupLine($"[red]Error: User with email '{email}' not found.[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[yellow]Found user: {user.FullName} ({user.EmailAddress})[/]");
            AnsiConsole.WriteLine();

            if (!AnsiConsole.Confirm("[yellow]Reset password for this user?[/]"))
            {
                AnsiConsole.MarkupLine("[blue]Operation cancelled.[/]");
                return 0;
            }

            var newPassword = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]New Password:[/]")
                    .PromptStyle("red")
                    .Secret());

            var confirmPassword = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]Confirm Password:[/]")
                    .PromptStyle("red")
                    .Secret());

            if (newPassword != confirmPassword)
            {
                AnsiConsole.MarkupLine("[red]Error: Passwords do not match.[/]");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                AnsiConsole.MarkupLine("[red]Error: Password must be at least 8 characters long.[/]");
                return 1;
            }


            var preHashedPassword = SHA512.HashData(System.Text.Encoding.UTF8.GetBytes(newPassword)).ToString();
            var hashedPassword = passwordService.HashPassword(preHashedPassword);

            user.PasswordHash = hashedPassword;
            user.LastUpdatedAt = DateTime.UtcNow;
            user.LastUpdatedBy = user.Id;

            await dbContext.SaveChangesAsync(cancellationToken);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓ Password reset successfully![/]");

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Field");
            table.AddColumn("Value");
            table.AddRow("User ID", user.Id.ToString());
            table.AddRow("Email", user.EmailAddress);
            table.AddRow("Name", user.FullName);
            table.AddRow("Updated", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"));

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]The user can now log in with the new password.[/]");

            return 0;
        }
    }
}
