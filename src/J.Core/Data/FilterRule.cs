using System.Text;

namespace J.Core.Data;

public readonly record struct FilterRule(
    FilterField Field,
    FilterOperator Operator,
    List<Tag>? TagValues,
    string? StringValue
)
{
    public override string ToString()
    {
        StringBuilder sb = new($"{Field.GetDisplayName()} {Operator.GetDisplayName(false)}");

        if (TagValues is not null)
        {
            sb.Append(' ');
            if (TagValues.Count == 1)
                sb.Append(TagValues[0].Name);
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
