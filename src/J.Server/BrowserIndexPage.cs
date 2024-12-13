using System.Net;
using System.Web;
using J.Core.Data;

namespace J.Server;

public static class BrowserIndexPage
{
    public static Html GenerateHtml(List<TagType> tagTypes, string host)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>Jackpot Media Library</title>
                <style>
                    {{BrowserShared.SharedCss}}
                </style>
            </head>
            <body>
                <h1>Jackpot Media Library</h1>
                <ul>
                    <li><a class="folder" href="http://{{host}}:777/browser-movies.html">Movies</a></li>
                    {{string.Join("\n", tagTypes.Select(TagTypeLi))}}
                </ul>
            </body>
            </html>
            """;

        string TagTypeLi(TagType t)
        {
            var query = HttpUtility.ParseQueryString("");
            query["tagTypeId"] = t.Id.Value;
            return $"<li><a class=\"folder\" href=\"http://{host}:777/browser-tagtype.html?{query}\">{WebUtility.HtmlEncode(t.PluralName)}</a></li>";
        }

        return new(html);
    }
}
