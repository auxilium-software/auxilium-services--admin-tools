using AuxiliumSoftware.AuxiliumServices.AdministrationTools.Seeding;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using Spectre.Console;
using System.Net.Mail;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Interaction;

internal static class Prompts
{
    public const int MinimumPasswordLength = 8;

    public static string AskRequiredText(string label, string? defaultValue = null)
    {
        var prompt = new TextPrompt<string>($"[green]{Markup.Escape(label)}:[/]")
            .Validate(v => string.IsNullOrWhiteSpace(v)
                ? ValidationResult.Error("[red]A value is required.[/]")
                : ValidationResult.Success()
            );

        if (!string.IsNullOrEmpty(defaultValue))
        {
            prompt.DefaultValue(defaultValue);
        }

        return AnsiConsole.Prompt(prompt).Trim();
    }

    public static string AskEmailAddress(string label = "Email address")
    {
        return AnsiConsole.Prompt(
            new TextPrompt<string>($"[green]{Markup.Escape(label)}:[/]")
                .Validate(v => MailAddress.TryCreate(v.Trim(), out var parsed) && parsed.Address == v.Trim()
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]That doesn't look like a valid email address.[/]")
                )
        ).Trim();
    }

    public static string AskNewPassword()
    {
        while (true)
        {
            var password = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]Password:[/]")
                    .Secret()
                    .Validate(v => v.Length >= MinimumPasswordLength
                        ? ValidationResult.Success()
                        : ValidationResult.Error($"[red]Password must be at least {MinimumPasswordLength} characters long.[/]")
                    )
            );

            var confirmation = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]Confirm password:[/]")
                    .Secret());

            if (password == confirmation)
            {
                return password;
            }

            AnsiConsole.MarkupLine("[red]Passwords do not match, try again.[/]");
        }
    }

    public static object AskSystemSetting(SystemSettingMetadata metadata, string? suggestedValue)
    {
        AnsiConsole.MarkupLineInterpolated($"[bold]{metadata.Name}[/]");
        if (!string.IsNullOrWhiteSpace(metadata.Description))
        {
            AnsiConsole.MarkupLineInterpolated($"[grey]{metadata.Description}[/]");
        }

        string label = metadata.Name;
        var defaultValue = suggestedValue ?? metadata.DefaultValue;

        object value = metadata.ValueType switch
        {
            SystemSettingValueTypeEnum.String  => AskRequiredText(label, defaultValue?.ToString()),
            SystemSettingValueTypeEnum.Int     => AskTyped<int>(label, defaultValue),
            SystemSettingValueTypeEnum.Decimal => AskTyped<decimal>(label, defaultValue),
            SystemSettingValueTypeEnum.Bool    => AnsiConsole.Confirm($"[green]{Markup.Escape(label)}?[/]", defaultValue as bool? ?? false),
            _ => throw new NotSupportedException($"Prompting for {metadata.ValueType} settings ('{metadata.Name}') isn't supported by the admin tools yet."),
        };

        AnsiConsole.WriteLine();
        return value;
    }

    private static T AskTyped<T>(string label, object? defaultValue) where T : notnull
    {
        var prompt = new TextPrompt<T>($"[green]{Markup.Escape(label)}:[/]");

        if (defaultValue is not null)
        {
            prompt.DefaultValue((T)Convert.ChangeType(defaultValue, typeof(T)));
        }

        return AnsiConsole.Prompt(prompt);
    }
}
