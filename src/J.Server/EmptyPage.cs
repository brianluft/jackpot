using System.Net;

namespace J.Server;

public static class EmptyPage
{
    public static Html GenerateHtml(string title, string messageHtml)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                {{PageShared.SharedHead}}
                <title>{{WebUtility.HtmlEncode(title)}}</title>
                <style>
                    {{PageShared.SharedCss}}
                    
                    div#message {
                        position: absolute;
                        top: 50%;
                        left: 50%;
                        transform: translate(-50%, -50%);
                        font-size: 18pt;
                        color: #c0c0c0;
                    }
                </style>
            </head>
            <body>
                <div id="message">
                    {{messageHtml}}
                </div>

                <script>
                    {{PageShared.GetSharedJs("", "empty", "", true)}}
                </script>
            </body>
            </html>
            """;

        return new(html);
    }
}
