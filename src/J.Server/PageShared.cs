namespace J.Server;

public static class PageShared
{
    public static string SharedCss { get; } =
        """
            ::-webkit-scrollbar {
                width: 14px;
            }

            ::-webkit-scrollbar-track, ::-webkit-scrollbar-corner {
                background: #1a1a1a;
            }

            ::-webkit-scrollbar-thumb {
                background: #444;
                border: 3px solid #1a1a1a;
                border-radius: 7px;
            }

            ::-webkit-scrollbar-thumb:hover {
                background: #555;
            }

            * {
                user-select: none;
            }

            body {
                margin: 0;
                background: #1a1a1a;
                color: #fff;
                font-family: system-ui;
            }
            """;

    public static string GetSharedJs(string sessionPassword) =>
        $$"""
            // Disable context menu page-wide
            document.addEventListener('contextmenu', e => e.preventDefault());

            // Capture Ctrl+F and / to focus the search box
            document.addEventListener('keydown', function(e) {
                if ((e.ctrlKey && e.key === 'f') || (!e.ctrlKey && e.key === '/')) {
                    e.preventDefault();
                    window.chrome.webview.postMessage(JSON.stringify({ Type: 'search' }));
                }
            });

            document.addEventListener('click', () => closeContextMenu());

            document.addEventListener('wheel', () => closeContextMenu());

            document.addEventListener('keydown', () => closeContextMenu());

            function openMovie(movieId) {
                window.chrome.webview.postMessage(JSON.stringify({ Type: 'play', Ids: [movieId] }));
            }

            function openTag(id) {
                location.href = '/tag.html?sessionPassword={{sessionPassword}}&tagId=' + encodeURIComponent(id) + '&pageIndex=0';
            }

            function open(id) {
                if (id.startsWith("movie-")) {
                    openMovie(id);
                } else if (id.startsWith("tag-")) {
                    openTag(id);
                } else {
                    alert('Invalid ID: ' + id);
                }
            }

            function menu(ids) {
                window.chrome.webview.postMessage(JSON.stringify({ Type: 'context-menu', Ids: ids }));
            }

            function closeContextMenu() {
                window.chrome.webview.postMessage(JSON.stringify({ Type: 'close-context-menu' }));
            }
            """;
}
