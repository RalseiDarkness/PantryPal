namespace PantryPal.Services;

public static class ApiBaseAddressSelector
{
    public const string ReleaseApiBaseAddress =
        "https://pantrypal-api-frgrbrbjcccbh3gn.westus-01.azurewebsites.net/";

    public static Uri Select(
        bool isDebug,
        string platform,
        string? debugOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        if (!isDebug)
        {
            return new Uri(ReleaseApiBaseAddress);
        }

        if (!string.IsNullOrWhiteSpace(debugOverride))
        {
            return ParseBaseAddress(debugOverride);
        }

        var address = platform.ToUpperInvariant() switch
        {
            "WINUI" or "WINDOWS" => "http://localhost:5222/",
            "MACCATALYST" or "MAC CATALYST" => "http://localhost:5222/",
            "ANDROID" => "http://10.0.2.2:5222/",
            "IOS" => "http://localhost:5222/",
            _ => throw new PlatformNotSupportedException(
                $"No Debug API address is configured for platform '{platform}'.")
        };

        return new Uri(address);
    }

    private static Uri ParseBaseAddress(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var address) ||
            (address.Scheme != Uri.UriSchemeHttp &&
             address.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(address.Query) ||
            !string.IsNullOrEmpty(address.Fragment))
        {
            throw new ArgumentException(
                "The Debug API base address must be an absolute HTTP or HTTPS URL.",
                nameof(value));
        }

        return new Uri(address.AbsoluteUri.TrimEnd('/') + "/");
    }
}
