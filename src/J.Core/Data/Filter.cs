namespace J.Core.Data;

public readonly record struct Filter(bool Or, List<FilterRule> Rules)
{
    public static Filter Default => new(false, []);
}
