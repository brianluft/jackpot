namespace J.Core.Data;

public sealed class TagTypeId : IdBase<TagTypeId>, IEquatable<TagTypeId>
{
    protected override string Prefix => "tagtype-";

    public TagTypeId()
        : base() { }

    public TagTypeId(string value)
        : base(value) { }

    protected override TagTypeId Create(string value) => new(value);
}
