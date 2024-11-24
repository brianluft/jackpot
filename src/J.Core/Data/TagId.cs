namespace J.Core.Data;

public sealed class TagId : IdBase<TagId>, IEquatable<TagId>
{
    private const string PREFIX = "tag-";

    protected override string Prefix => PREFIX;

    public TagId()
        : base() { }

    public TagId(string value)
        : base(value) { }

    protected override TagId Create(string value) => new(value);

    public static bool HasPrefix(string value) => value.StartsWith(PREFIX);
}
