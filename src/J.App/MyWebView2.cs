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
            settings.AreBrowserAcceleratorKeysEnabled = true;
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
                // Don't cache documents.
                CoreWebView2.AddWebResourceRequestedFilter(
                    "*",
                    CoreWebView2WebResourceContext.Document,
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
}
