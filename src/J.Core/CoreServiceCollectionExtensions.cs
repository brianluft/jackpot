using J.Core;

namespace Microsoft.Extensions.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public static void AddCore(this IServiceCollection services)
    {
        services.AddSingleton<AccountSettingsProvider>();
        services.AddSingleton<LibraryProvider>();
        services.AddSingleton<ProcessTempDir>();
    }
}
