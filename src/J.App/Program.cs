using Microsoft.Extensions.DependencyInjection;

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
