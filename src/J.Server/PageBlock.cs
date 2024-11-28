using J.Core.Data;

namespace J.Server;

public readonly record struct PageBlock(
    MovieId MovieId, // movie to get the clip from
    TagId? TagId, // if targeting a tag instead of a movie on click, this is the tag
    TagTypeId? UntaggedTagTypeId, // if targeting movies that are not tagged with a tag type, this is that tag type
    string Title,
    DateTimeOffset Date,
    Dictionary<TagTypeId, string> SortTags,
    Dictionary<string, string> Metadata
);
