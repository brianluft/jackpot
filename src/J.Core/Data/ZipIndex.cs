namespace J.Core.Data;

public readonly record struct ZipIndex(List<ZipEntryLocation> Entries, OffsetLength ZipHeader);
