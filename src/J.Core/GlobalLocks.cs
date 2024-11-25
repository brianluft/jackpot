namespace J.Core;

public static class GlobalLocks
{
    public static Lock BigCpu { get; } = new();
}
