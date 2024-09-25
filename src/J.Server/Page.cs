using System.Net;
using System.Text;
using System.Web;
using J.Core.Data;

namespace J.Server;

public readonly record struct Page(List<Page.Block> Blocks, string Title)
{
    public readonly record struct Block(
        MovieId MovieId, // movie to get the clip from
        TagId? TagId, // if targeting a tag instead of a movie on click, this is the tag
        string Title
    );

    public string ToHtml()
    {
        StringBuilder rows = new();

        foreach (var row in Blocks.Chunk(5))
        {
            rows.Append("<tr>");
            foreach (var block in row)
            {
                var query = HttpUtility.ParseQueryString("");
                query["movieId"] = block.MovieId.Value;
                var onclick = block.TagId is null
                    ? $"openMovie('{block.MovieId.Value}');return false;"
                    : $"openTag('{block.TagId.Value}');return false;";
                rows.Append(
                    $"""
                    <td>
                        <div class="video-container">
                            <video autoplay loop muted playsinline onclick="{onclick}">
                                <source src="/clip.mp4?{query}" type="video/mp4">
                            </video>
                            <div class="video-title-container">
                                <div class="video-title" onclick="{onclick}">{WebUtility.HtmlEncode(block.Title)}</div>
                            </div>
                        </div>
                    </td>
                    """
                );
            }
            rows.Append("</tr>");
        }

        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <title>{{WebUtility.HtmlEncode(Title)}}</title>
                <style>
                    body {
                        margin: 0;
                        padding: 0;
                        background-color: black;
                        user-select: none;
                    }
                    table {
                        border-collapse: collapse;
                    }
                    table, tr, td {
                        padding: 0;
                        margin: 0;
                        line-height: 0;
                    }
                    div.video-container {
                        position: relative;
                        padding: 0;
                        margin: 0;
                        cursor: pointer;
                        background: black;
                        width: 20vw;
                        height: 20vh;
                        overflow: hidden;
                    }
                    div.video-title-container {
                        position: absolute;
                        left: 0;
                        top: calc(20vh - 20px);
                        width: 20vw;
                        height: 20px;
                        margin: 0;
                        padding: 0;
                        background: rgba(0, 0, 0, 0.5);
                    }
                    div.video-title {
                        color: white;
                        font-family: 'Segoe UI';
                        font-size: 12px;
                        font-weight: bold;
                        text-align: center;
                        padding-top: 9px;
                        padding-left: 10px;
                        padding-right: 10px;
                        height: 15px;
                        overflow: hidden;
                        text-overflow: ellipsis;
                        white-space: nowrap;
                    }
                    video {
                        width: 20vw;
                        height: 20vh;
                        object-fit: contain;
                        object-position: center;
                        margin: 0;
                        padding: 0;
                    }
                </style>
                <script>
                    function openMovie(movieId) {
                        // Escape the movieId to safely use it in the URL
                        const escapedMovieId = encodeURIComponent(movieId);

                        // Create the full URL with the escaped movieId
                        const url = `/open-movie?movieId=${escapedMovieId}`;

                        // Send a POST request
                        fetch(url, { method: 'POST' })
                        .then(() => {
                            // cool
                        })
                        .catch(error => {
                            alert('Could not open movie: ' + error.message);
                        });
                    }

                    function openTag(id) {
                        location.href = '/tag.html?tagId=' + encodeURIComponent(id) + '&pageIndex=0';
                    }

                    document.addEventListener('contextmenu', function(event) {
                        event.preventDefault();
                    });
                </script>
            </head>
            <body>
                <table>
                    {{rows}}
                </table>
            </body>
            </html>
            """;

        return html;
    }
}
