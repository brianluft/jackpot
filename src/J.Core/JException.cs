namespace J.Core;

public sealed class JException(string message, string? wikiUrl = null) : Exception(message)
{
    public string? WikiUrl => wikiUrl;
}
