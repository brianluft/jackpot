using System.Net;
using System.Web;
using J.Core.Data;

namespace J.Server;

public static class BrowserTagTypePage
{
    public static Html GenerateHtml(TagType tagType, List<Tag> tags, string host)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>{{WebUtility.HtmlEncode(tagType.PluralName)}}</title>
                <style>
                    {{BrowserShared.SharedCss}}
                </style>
            </head>
            <body>
                <h1>{{WebUtility.HtmlEncode(tagType.PluralName)}}</h1>
                <ul>
                    <li><a class="back" href="http://{{host}}:777/">Back</a></li>
                    {{string.Join("\n", tags.OrderBy(x => x.Name).Select(TagLi))}}
                </ul>
            </body>
            </html>
            """;

        string TagLi(Tag t)
        {
            var query = HttpUtility.ParseQueryString("");
            query["tagTypeId"] = tagType.Id.Value;
            query["tagId"] = t.Id.Value;
            return $"<li><a class=\"folder\" href=\"http://{host}:777/browser-tag.html?{WebUtility.HtmlEncode(query.ToString())}\">{WebUtility.HtmlEncode(t.Name)}</a></li>";
        }

        return new(html);
    }
}
