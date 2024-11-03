using System;
using J.Base;
using J.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace J.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        TaskbarUtil.SetTaskbarAppId();

        ServiceCollection services = new();

        services.AddHttpClient(
            typeof(Client).FullName!,
            h =>
            {
                h.Timeout = TimeSpan.FromMilliseconds(5000);
            }
        );
        services.AddCore();

        // Configure a custom user data folder for WebView2.
        // Otherwise, WebView2 fails when it tries to store a cache in the app directory, which is read only.
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
        services.AddSingleton<SingleInstanceManager>();
        services.AddSingleton<MyApplicationContext>();

        services.AddTransient<LoginForm>();
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

        var singleInstanceManager = serviceProvider.GetRequiredService<SingleInstanceManager>();
        if (!singleInstanceManager.IsFirstInstance)
        {
            singleInstanceManager.ActivateFirstInstance();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(serviceProvider.GetRequiredService<MyApplicationContext>());
    }

    private sealed class MyApplicationContext : ApplicationContext
    {
        private readonly AccountSettingsProvider _accountSettingsProvider;
        private readonly LibraryProvider _libraryProvider;
        private readonly Client _client;
        private readonly M3u8FolderSync _m3u8FolderSync;
        private readonly IServiceProvider _serviceProvider;

        public MyApplicationContext(
            AccountSettingsProvider accountSettingsProvider,
            LibraryProvider libraryProvider,
            Client client,
            M3u8FolderSync m3U8FolderSync,
            IServiceProvider serviceProvider
        )
        {
            _accountSettingsProvider = accountSettingsProvider;
            _libraryProvider = libraryProvider;
            _client = client;
            _m3u8FolderSync = m3U8FolderSync;
            _serviceProvider = serviceProvider;

            if (!_accountSettingsProvider.Current.AppearsValid)
                ShowLoginForm();
            else
                ShowConnectForm();
        }

        private void ShowLoginForm()
        {
            var f = _serviceProvider.GetRequiredService<LoginForm>();
            f.FormClosed += delegate
            {
                if (f.DialogResult == DialogResult.OK)
                    ShowConnectForm();
                else
                    ExitThread();
            };
            f.Show();
        }

        private void ShowConnectForm()
        {
            SimpleProgressForm f =
                new(
                    (updateProgress, updateMessage, cancel) =>
                    {
                        updateMessage("Connecting...");
                        _libraryProvider.Connect();
                        updateMessage("Synchronizing library...");
                        _libraryProvider.SyncDownAsync(cancel).GetAwaiter().GetResult();
                        updateMessage("Starting background service...");
                        _client.Start();
                        updateMessage("Synchronizing .m3u8 folder...");
                        _m3u8FolderSync.Sync();
                    },
                    false
                )
                {
                    Text = "Jackpot Login",
                };
            f.FormClosed += delegate
            {
                switch (f.DialogResult)
                {
                    case DialogResult.Cancel:
                        ShowLoginForm();
                        break;
                    case DialogResult.Abort:
                        MessageBox.Show(
                            "Jackpot login failed.\n\n" + f.Exception!.SourceException.Message,
                            "Jackpot Login",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        ShowLoginForm();
                        break;
                    case DialogResult.OK:
                        ShowMainForm();
                        break;
                    default:
                        ExitThread();
                        break;
                }
            };
            f.Show();
        }

        private void ShowMainForm()
        {
            var f = _serviceProvider.GetRequiredService<MainForm>();
            f.FormClosed += delegate
            {
                if (f.DialogResult == DialogResult.Retry)
                    ShowLoginForm();
                else
                    ExitThread();
            };
            f.Show();
        }
    }
}
