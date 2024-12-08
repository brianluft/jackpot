using System.Net;

namespace J.Server;

public static class MoviePlayerPage
{
    public static Html GenerateHtml(string title, string m3u8Url)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>{{WebUtility.HtmlEncode(title)}}</title>
                <script src="/static/hls.min.js"></script>
                <style>
                    body { 
                        margin: 0;
                        padding: 0;
                        height: 100vh;
                        width: 100vw;
                        display: flex;
                        overflow: hidden;
                        background: #000;
                    }

                    .video-container {
                        width: 100vw;
                        height: 100vh;
                    }

                    video {
                        width: 100%;
                        height: 100%;
                    }
                </style>
            </head>
            <body>
                <div class="video-container">
                    <video id="player" controls autoplay muted></video>
                </div>

                <script>
                    const video = document.getElementById('player');
                    const hls = new Hls();
                    hls.loadSource('{{m3u8Url}}');
                    hls.attachMedia(video);
                </script>
            </body>
            </html>
            """;

        return new(html);
    }
}
