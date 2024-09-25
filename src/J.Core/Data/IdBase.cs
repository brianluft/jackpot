using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace J.Core.Data;

public abstract class IdBase<T>
    where T : IdBase<T>
{
    private static readonly char[] VALID_CHARS = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    protected abstract string Prefix { get; }

    private string _value;

    public string Value
    {
        get => _value;
        init
        {
            if (value.StartsWith(Prefix))
                _value = value;
            else
                throw new ArgumentException($"Invalid {typeof(T).Name}.");
        }
    }

    public IdBase()
    {
        StringBuilder sb = new(Prefix.Length + 16);
        sb.Append(Prefix);
        for (int i = 0; i < 16; i++)
            sb.Append(VALID_CHARS[Random.Shared.Next(0, VALID_CHARS.Length)]);
        _value = sb.ToString();
    }

    public IdBase(string value)
    {
        if (!value.StartsWith(Prefix))
            throw new ArgumentException($"Invalid {typeof(T).Name}.");

        _value = value;
    }

    public bool TryParse(string value, [MaybeNullWhen(false)] out T id)
    {
        if (value.StartsWith(Prefix))
        {
            id = Create(value);
            return true;
        }

        id = null;
        return false;
    }

    protected abstract T Create(string value);

    public override bool Equals(object? obj) => obj is IdBase<T> other && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();

    public bool Equals(T? other) => other is not null && Value == other.Value;

    public static bool operator ==(IdBase<T> left, IdBase<T> right) => left.Equals(right);

    public static bool operator !=(IdBase<T> left, IdBase<T> right) => !left.Equals(right);
}
