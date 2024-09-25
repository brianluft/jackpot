namespace J.Core.Data;

public sealed class TagId : IdBase<TagId>, IEquatable<TagId>
{
    protected override string Prefix => "tag-";

    public TagId()
        : base() { }

    public TagId(string value)
        : base(value) { }

    protected override TagId Create(string value) => new(value);
}
