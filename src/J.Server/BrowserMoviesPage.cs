using System.Net;
using System.Web;
using J.Core.Data;

namespace J.Server;

public static class BrowserMoviesPage
{
    public static Html GenerateHtml(List<Movie> movies, string sessionPassword)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>Movies</title>
                <style>
                    {{ExternalShared.SharedCss}}
                </style>
            </head>
            <body>
                <h1>Movies</h1>
                <ul>
                    <li><a class="back" href="/">Back</a></li>
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
            return $"<li><a class=\"file\" href=\"/movie-play.html?{WebUtility.HtmlEncode(query.ToString())}\">{WebUtility.HtmlEncode(m.Filename)}</a></li>";
        }

        return new(html);
    }
}
