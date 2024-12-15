using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace J.App;

public sealed class MyWebView2 : WebView2
{
    public bool IsCoreWebView2Initialized { get; private set; }

    public MyWebView2()
    {
        CoreWebView2InitializationCompleted += delegate
        {
            var settings = CoreWebView2.Settings;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.AreDefaultContextMenusEnabled = true;
            settings.AreDefaultScriptDialogsEnabled = false;
            settings.AreHostObjectsAllowed = false;
            settings.IsBuiltInErrorPageEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            settings.IsNonClientRegionSupportEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsPinchZoomEnabled = false;
            settings.IsReputationCheckingRequired = false;
            settings.IsScriptEnabled = true;
            settings.IsStatusBarEnabled = false;
            settings.IsSwipeNavigationEnabled = false;
            settings.IsWebMessageEnabled = true;
            settings.IsZoomControlEnabled = false;

#if DEBUG
            settings.AreDevToolsEnabled = true;
#else
            settings.AreDevToolsEnabled = false;
#endif

            BeginInvoke(() =>
            {
                // Avoid caching anything.
                CoreWebView2.AddWebResourceRequestedFilter(
                    "*",
                    CoreWebView2WebResourceContext.All,
                    CoreWebView2WebResourceRequestSourceKinds.All
                );

                CoreWebView2.WebResourceRequested += (sender, e) =>
                {
                    e.Request.Headers.SetHeader("Cache-Control", "no-cache, no-store, must-revalidate");
                    e.Request.Headers.SetHeader("Pragma", "no-cache");
                    e.Request.Headers.SetHeader("Expires", "0");
                };
            });

            IsCoreWebView2Initialized = true;
        };

        _ = EnsureCoreWebView2Async(Program.SharedCoreWebView2Environment);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // On F5 or Ctrl+R, reload the page.
        // On Alt+Left or Alt+Right, navigate back or forward.

        if (e.KeyCode == Keys.F5 || (e.KeyCode == Keys.R && e.Control))
        {
            CoreWebView2.Reload();
            e.Handled = true;
        }
        else if (e.Alt && e.KeyCode == Keys.Left)
        {
            CoreWebView2.GoBack();
            e.Handled = true;
        }
        else if (e.Alt && e.KeyCode == Keys.Right)
        {
            CoreWebView2.GoForward();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }
}
