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
                -webkit-font-smoothing: subpixel-antialiased;
            }

            body {
                margin: 0;
                background: #1a1a1a;
                color: #fff;
                font-family: system-ui;
            }
            """;

    public static string SharedHead { get; } =
        """
            <meta charset="utf-8">
            <link href="/static/tabulator.min.css" rel="stylesheet">
            <script src="/static/tabulator.min.js"></script>
            """;

    public static string GetSharedJs(string sessionPassword, string cookieName) =>
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

            const cookieName = '{{cookieName}}';
            function saveAndRestoreScrollPosition(target, options = {}) {
                const isTabulator = target instanceof Tabulator;

                if (!target) {
                    console.warn('No valid target provided to saveAndRestoreScrollPosition');
                    return;
                }

                const defaultOptions = {
                    readyEvent: isTabulator ? {
                        target: target,
                        eventName: 'tableBuilt'
                    } : {
                        target: window,
                        eventName: 'load'
                    }
                };

                const config = { ...defaultOptions, ...options };

                // Cookie management functions
                function setCookie(name, value, days = 7) {
                    const expires = new Date();
                    expires.setTime(expires.getTime() + days * 24 * 60 * 60 * 1000);
                    document.cookie = `${name}=${value};expires=${expires.toUTCString()};path=/`;
                }

                function getCookie(name) {
                    const keyValue = document.cookie
                        .split('; ')
                        .find(row => row.startsWith(name + '='));
                    return keyValue ? keyValue.split('=')[1] : null;
                }

                // Scroll position management
                let isThrottled = false;

                function getScrollTop() {
                    return isTabulator ? target.rowManager.element.scrollTop : target.scrollTop;
                }

                function setScrollTop(value) {
                    if (isTabulator) {
                        target.rowManager.element.scrollTop = value;
                    } else {
                        target.scrollTop = value;
                    }
                }

                function saveScrollPosition(scrollTop) {
                    // For Tabulator, scrollTop is provided by the event
                    const offset = Math.round(isTabulator ? scrollTop : getScrollTop());
                    setCookie(cookieName + '_scroll', offset);
                }

                // Throttled scroll handler for non-Tabulator scenarios
                const regularScrollHandler = function() {
                    if (isThrottled) {
                        return;
                    }
                    isThrottled = true;

                    saveScrollPosition();
                    setTimeout(function() {
                        isThrottled = false;
                        saveScrollPosition();
                    }, 250);
                };

                // Set up scroll listeners based on context
                if (isTabulator) {
                    target.on('scrollVertical', saveScrollPosition);
                } else {
                    target.addEventListener('scroll', regularScrollHandler, { passive: true });
                }

                // Additional save points to ensure scroll position isn't lost
                window.addEventListener('beforeunload', () => saveScrollPosition(getScrollTop()));
                window.addEventListener('visibilitychange', function() {
                    if (document.visibilityState === 'hidden') {
                        saveScrollPosition(getScrollTop());
                    }
                });

                // Restore scroll position when ready
                function restoreScrollPosition() {
                    const savedScrollY = getCookie(cookieName + '_scroll');
                    if (savedScrollY !== null) {
                        // Small delay to ensure content is laid out
                        setTimeout(() => {
                            setScrollTop(parseInt(savedScrollY, 10));
                        }, 0);
                    }
                }

                // Handle ready event based on context
                const { target: readyTarget, eventName: readyEventName } = config.readyEvent;

                if (document.readyState === 'complete') {
                    restoreScrollPosition();
                } else if (isTabulator) {
                    readyTarget.on(readyEventName, restoreScrollPosition);
                } else {
                    readyTarget.addEventListener(readyEventName, restoreScrollPosition);
                }

                // Return cleanup function
                return function cleanup() {
                    if (isTabulator) {
                        target.off('scrollVertical', saveScrollPosition);
                        readyTarget.off(readyEventName, restoreScrollPosition);
                    } else {
                        target.removeEventListener('scroll', regularScrollHandler);
                        readyTarget.removeEventListener(readyEventName, restoreScrollPosition);
                    }
                    window.removeEventListener('beforeunload', () => saveScrollPosition(getScrollTop()));
                    window.removeEventListener('visibilitychange', saveScrollPosition);
                };
            }
            """;
}
