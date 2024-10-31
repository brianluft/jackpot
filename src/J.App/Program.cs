using J.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace J.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        ServiceCollection services = new();

        services.AddHttpClient(
            typeof(Client).FullName!,
            h =>
            {
                h.Timeout = TimeSpan.FromMilliseconds(5000);
            }
        );
        services.AddCore();

        services.AddSingleton(services =>
        {
            var processTempDir = services.GetRequiredService<ProcessTempDir>();
            var userDataDir = Path.Combine(processTempDir.Path, "WebView2");
            Directory.CreateDirectory(userDataDir);

            CoreWebView2EnvironmentOptions options = new();
            return CoreWebView2Environment
                .CreateAsync(browserExecutableFolder: null, userDataFolder: userDataDir, options: options)
                .GetAwaiter()
                .GetResult();
        });

        services.AddSingleton<Client>();
        services.AddSingleton<S3Uploader>();

        services.AddTransient<AccountSettingsForm>();
        services.AddTransient<EditMoviesChooseTagForm>();
        services.AddTransient<EditMoviesForm>();
        services.AddTransient<EditMoviesRemoveTagForm>();
        services.AddTransient<EditTagForm>();
        services.AddTransient<EditTagsForm>();
        services.AddTransient<FilterChooseTagForm>();
        services.AddTransient<FilterEnterStringForm>();
        services.AddTransient<Importer>();
        services.AddTransient<ImportForm>();
        services.AddTransient<ImportProgressFormFactory>();
        services.AddTransient<LibraryProviderAdapter>();
        services.AddTransient<M3u8FolderSync>();
        services.AddTransient<MainForm>();
        services.AddTransient<MovieEncoder>();
        services.AddTransient<MovieExporter>();

        using var serviceProvider = services.BuildServiceProvider();
        ApplicationConfiguration.Initialize();
        Application.Run(serviceProvider.GetRequiredService<MainForm>());
    }
}
