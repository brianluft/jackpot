using System.Collections.Specialized;
using System.Web;

namespace J.Server;

#pragma warning disable IDE1006 // Naming Styles
public readonly record struct PageBlockJson(string id, string url, string title, Dictionary<string, string> metadata)
#pragma warning restore IDE1006 // Naming Styles
{
    public PageBlockJson(PageBlock block, string sessionPassword)
        : this(
            id: block.TagId?.Value ?? block.MovieId.Value,
            url: $"/clip.mp4?{GetQuery(block, sessionPassword)}",
            title: block.Title,
            metadata: block.Metadata
        ) { }

    private static NameValueCollection GetQuery(PageBlock block, string sessionPassword)
    {
        var query = HttpUtility.ParseQueryString("");
        query["sessionPassword"] = sessionPassword;
        query["movieId"] = block.MovieId.Value;
        return query;
    }
}
