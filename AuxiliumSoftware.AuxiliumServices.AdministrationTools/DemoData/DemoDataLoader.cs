using AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData;

internal static class DemoDataLoader
{
    public static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, "DemoData", "demo-data.jsonc");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(allowIntegerValues: false),
        },
    };

    public static async Task<(DemoDataDefinition? Definition, string? Error)> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return (null, $"Demo data file '{path}' does not exist.");
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var definition = await JsonSerializer.DeserializeAsync<DemoDataDefinition>(stream, SerializerOptions, cancellationToken);

            return definition is null
                ? (null, $"Demo data file '{path}' is empty.")
                : (definition, null);
        }
        catch (JsonException ex)
        {
            return (null, $"Demo data file '{path}' is invalid at {ex.Path} (line {ex.LineNumber + 1}): {ex.Message}");
        }
    }
}
