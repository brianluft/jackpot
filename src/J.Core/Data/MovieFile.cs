namespace J.Core.Data;

public readonly record struct MovieFile(MovieId MovieId, string Name, OffsetLength OffsetLength, byte[]? Data);
