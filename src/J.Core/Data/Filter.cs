namespace J.Core.Data;

public readonly record struct Filter(bool Or, List<FilterRule> Rules, string Search)
{
    public static Filter Default => new(false, [], "");

    public bool IsDefault => Or == false && Rules.Count == 0 && Search == "";
}
