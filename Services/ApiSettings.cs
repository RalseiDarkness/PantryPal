
namespace PantryPal.Services;

public static class ApiSettings
{
    private const string BaseAddressPreferenceKey = "PantryPal.ApiBaseAddress";

#if DEBUG
    private const string MacCatalystDevelopmentBaseAddress = "http://localhost:5222/";
    private const string AndroidEmulatorDevelopmentBaseAddress = "http://10.0.2.2:5222/";
    private const string WindowsDevelopmentBaseAddress = "http://localhost:5222/";
#endif

    public static Uri GetBaseAddress()
    {
        var configuredAddress = Preferences.Default.Get(
            BaseAddressPreferenceKey,
            string.Empty);

        if (!string.IsNullOrWhiteSpace(configuredAddress))
        {
            return ValidateBaseAddress(configuredAddress);
        }

#if DEBUG
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return new Uri(AndroidEmulatorDevelopmentBaseAddress);
        }

        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            return new Uri(WindowsDevelopmentBaseAddress);
        }

        if (DeviceInfo.Platform == DevicePlatform.MacCatalyst)
        {
            return new Uri(MacCatalystDevelopmentBaseAddress);
        }

        return new Uri(MacCatalystDevelopmentBaseAddress);
#else
        throw new InvalidOperationException(
            "Configure the PantryPal API base address before searching for recipes.");
#endif
    }

    public static void SetBaseAddress(string baseAddress)
    {
        var validatedAddress = ValidateBaseAddress(baseAddress);
        Preferences.Default.Set(BaseAddressPreferenceKey, validatedAddress.AbsoluteUri);
    }

    private static Uri ValidateBaseAddress(string baseAddress)
    {
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var address) ||
            (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "The API base address must be an absolute HTTP or HTTPS URL.",
                nameof(baseAddress));
        }

#if !DEBUG
        if (address.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "The API must use HTTPS outside of development builds.",
                nameof(baseAddress));
        }
#endif

        return new Uri(address.AbsoluteUri.TrimEnd('/') + "/");
    }
}
