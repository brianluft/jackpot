using System.Net;
using System.Web;
using J.Core.Data;

namespace J.Server;

public static class BrowserTagPage
{
    public static Html GenerateHtml(
        TagTypeId tagTypeId,
        Tag tag,
        List<Movie> movies,
        string sessionPassword,
        string host
    )
    {
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>{{WebUtility.HtmlEncode(tag.Name)}}</title>
                <style>
                    {{BrowserShared.SharedCss}}
                </style>
            </head>
            <body>
                <h1>{{WebUtility.HtmlEncode(tag.Name)}}</h1>
                <ul>
                    <li><a class="back" href="http://{{host}}:777/browser-tagtype.html?tagTypeId={{WebUtility.HtmlEncode(
                tagTypeId.Value
            )}}">Back</a></li>
                    {{string.Join("\n", movies.OrderBy(x => x.Filename).ThenBy(x => x.DateAdded).Select(MovieLi))}}
                </ul>
            </body>
            </html>
            """;

        string MovieLi(Movie m)
        {
            var query = HttpUtility.ParseQueryString("");
            query["movieId"] = m.Id.Value;
            query["sessionPassword"] = sessionPassword;
            query["host"] = host;
            return $"<li><a class=\"file\" href=\"http://{host}:777/movie-play.html?{WebUtility.HtmlEncode(query.ToString())}\">{WebUtility.HtmlEncode(m.Filename)}</a></li>";
        }

        return new(html);
    }
}
