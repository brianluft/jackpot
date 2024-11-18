namespace J.Core.Data;

public readonly record struct SortOrder(bool Shuffle, bool Ascending, string Field)
{
    public static SortOrder Default => new(false, true, "name");
}
