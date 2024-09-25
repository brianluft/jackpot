namespace J.Core.Data;

public sealed class MovieId : IdBase<MovieId>, IEquatable<MovieId>
{
    protected override string Prefix => "movie-";

    public MovieId()
        : base() { }

    public MovieId(string value)
        : base(value) { }

    protected override MovieId Create(string value) => new(value);
}
