// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Configuration;

public static class ConfigurationExtension
{
    public static IConfigurationBuilder AddAppsettingsCommon(this ConfigurationManager configuration)
    {
        return configuration
            .AddJsonFile("appsettings.common.json", true, true)
            .AddJsonFile("appsettings.environment.json", true, true);
    }
}
