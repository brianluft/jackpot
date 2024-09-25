namespace J.Core.Data;

public readonly record struct Movie(MovieId Id, string Filename, string S3Key, DateTimeOffset DateAdded);
