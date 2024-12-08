using System.Web;
using J.Core.Data;

namespace J.Server;

public static class MoviePreviewPage
{
    public static Html GenerateHtml(MovieId movieId, string sessionPassword, string filterSortHash)
    {
        var query = HttpUtility.ParseQueryString("");
        query["sessionPassword"] = sessionPassword;
        query["movieId"] = movieId.Value;

        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                {{PageShared.SharedHead}}
                <style>
                    {{PageShared.SharedCss}}

                    body, html {
                        width: 100%;
                        height: 100%;
                        overflow: hidden;
                        background: black !important;
                    }

                    .video-container {
                        width: 100%;
                        height: 100vh;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                    }

                    video {
                        width: 100%;
                        height: 100%;
                        object-fit: contain;
                    }
                </style>
            </head>
            <body>
                <div class="video-container">
                    <video
                        autoplay
                        loop
                        muted
                        playsinline
                        disablepictureinpicture
                        disableremoteplayback
                    >
                        <source src="/clip.mp4?{{query}}" type="video/mp4">
                    </video>
                </div>
                <script>
                    {{PageShared.GetSharedJs(sessionPassword, nameof(MoviePreviewPage), filterSortHash, true)}}
                </script>
            </body>
            </html>
            """;

        return new(html);
    }
}
