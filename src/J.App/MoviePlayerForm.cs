using J.Core.Data;
using Microsoft.Web.WebView2.WinForms;

namespace J.App;

public sealed class MoviePlayerForm : Form
{
    private readonly Client _client;
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly WebView2 _browser;
    private CompleteWindowState _lastWindowState;
    private bool isFullScreen = false;

    public MoviePlayerForm(Client client, LibraryProviderAdapter libraryProvider)
    {
        _client = client;
        _libraryProvider = libraryProvider;
        Ui ui = new(this);

        Controls.Add(_browser = ui.NewWebView2());
        {
            _browser.Dock = DockStyle.Fill;
            _browser.CoreWebView2InitializationCompleted += delegate
            {
                _browser.CoreWebView2.ContainsFullScreenElementChanged += CoreWebView2_ContainsFullScreenElementChanged;
            };
        }

        Size = ui.GetSize(1600, 900);
        CenterToScreen();
        FormBorderStyle = FormBorderStyle.Sizable;
        WindowState = FormWindowState.Maximized;
        Icon = ui.GetIconResource("App.ico");
        ShowIcon = true;
        ShowInTaskbar = true;
    }

    public void Initialize(MovieId movieId)
    {
        var movie = _libraryProvider.GetMovie(movieId);
        Text = movie.Filename;

        var url = _client.GetMoviePlayerUrl(movieId);
        _browser.Source = new(url);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _browser.Focus();
    }

    private void CoreWebView2_ContainsFullScreenElementChanged(object? sender, object? e)
    {
        var fullscreen = _browser.CoreWebView2.ContainsFullScreenElement;

        // Ensure we're running on the UI thread
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => HandleFullScreenChange(fullscreen)));
        }
        else
        {
            HandleFullScreenChange(fullscreen);
        }
    }

    private void HandleFullScreenChange(bool enterFullScreen)
    {
        if (enterFullScreen && !isFullScreen)
        {
            // Store current window state
            _lastWindowState = CompleteWindowState.Save(this);

            // Enter full screen
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            isFullScreen = true;
        }
        else if (!enterFullScreen && isFullScreen)
        {
            // Restore previous window state
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
            _lastWindowState.Restore(this);
            isFullScreen = false;
        }
    }
}
