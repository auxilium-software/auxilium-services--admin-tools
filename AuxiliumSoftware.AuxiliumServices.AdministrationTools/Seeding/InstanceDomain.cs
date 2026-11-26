namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Seeding;

internal static class InstanceDomain
{
    // the maximum length of a DNS host name
    public const int MaxLength = 253;

    public static string Normalise(string domain) =>
        domain.Trim().TrimEnd('.').ToLowerInvariant();

    public static bool IsValid(string normalisedDomain, out string? error)
    {
        if (string.IsNullOrWhiteSpace(normalisedDomain))
        {
            error = "Domain cannot be empty.";
            return false;
        }

        if (normalisedDomain.Length > MaxLength)
        {
            error = $"Domain cannot be longer than {MaxLength} characters.";
            return false;
        }

        if (normalisedDomain.Contains("://") || normalisedDomain.Contains('/'))
        {
            error = "Enter a bare host name (e.g. portal.example.org), not a URL.";
            return false;
        }

        if (Uri.CheckHostName(normalisedDomain) != UriHostNameType.Dns)
        {
            error = $"'{normalisedDomain}' is not a valid DNS host name.";
            return false;
        }

        error = null;
        return true;
    }
}
