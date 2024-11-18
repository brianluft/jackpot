using System.Text;

namespace J.Core.Data;

public readonly record struct FilterRule(
    FilterField Field,
    FilterOperator Operator,
    List<TagId>? TagValues,
    string? StringValue
)
{
    public string GetDisplayName(Dictionary<TagTypeId, TagType> tagTypes, Dictionary<TagId, string> tagNames)
    {
        TagType? filterFieldTagType = null;
        if (Field.Type == FilterFieldType.TagType)
            filterFieldTagType = tagTypes[Field.TagTypeId!];
        StringBuilder sb = new($"{Field.GetDisplayName(filterFieldTagType)} {Operator.GetDisplayName(false)}");

        if (TagValues is not null)
        {
            sb.Append(' ');
            if (TagValues.Count == 1)
                sb.Append(tagNames[TagValues[0]]);
            else
                sb.Append($"({TagValues.Count} values)");
        }
        else if (StringValue is not null)
        {
            sb.Append(" \"");
            if (StringValue.Length > 50)
                sb.Append(StringValue[..50] + "...");
            else
                sb.Append(StringValue);
            sb.Append('"');
        }

        return sb.ToString();
    }
}
