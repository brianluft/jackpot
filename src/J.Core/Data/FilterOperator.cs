namespace J.Core.Data;

public enum FilterOperator
{
    IsTag,
    IsNotTag,
    IsTagged,
    IsNotTagged,
    ContainsString,
    DoesNotContainString,
}

public static class FilterOperatorExtensions
{
    public static string GetDisplayName(this FilterOperator x, bool ellipsis)
    {
        var e = ellipsis ? "..." : "";
        return x switch
        {
            FilterOperator.IsTag => $"is{e}",
            FilterOperator.IsNotTag => $"is not{e}",
            FilterOperator.IsTagged => "is tagged",
            FilterOperator.IsNotTagged => "is not tagged",
            FilterOperator.ContainsString => $"contains{e}",
            FilterOperator.DoesNotContainString => $"does not contain{e}",
            _ => throw new ArgumentOutOfRangeException(nameof(x)),
        };
    }

    public static bool RequiresTagInput(this FilterOperator x) => x is FilterOperator.IsTag or FilterOperator.IsNotTag;

    public static bool RequiresStringInput(this FilterOperator x) =>
        x is FilterOperator.ContainsString or FilterOperator.DoesNotContainString;
}
