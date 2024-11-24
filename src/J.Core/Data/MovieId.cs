namespace J.Core.Data;

public sealed class MovieId : IdBase<MovieId>, IEquatable<MovieId>
{
    private const string PREFIX = "movie-";

    protected override string Prefix => PREFIX;

    public MovieId()
        : base() { }

    public MovieId(string value)
        : base(value) { }

    protected override MovieId Create(string value) => new(value);

    public static bool HasPrefix(string value) => value.StartsWith(PREFIX);
}
