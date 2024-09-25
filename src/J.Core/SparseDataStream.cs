namespace J.Core;

public sealed class SparseDataStream : Stream
{
    private long _position;
    private readonly long _length;
    private readonly List<Range> _dataRanges;

    public SparseDataStream(long length, List<Range> dataRanges)
    {
        _position = 0;
        _length = length;
        _dataRanges = dataRanges;

        // Sort dataRanges by Offset.
        _dataRanges.Sort((a, b) => a.Offset.CompareTo(b.Offset));
    }

    public override bool CanRead => true;
    public override bool CanWrite => false;
    public override bool CanSeek => true;

    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > _length)
                throw new ArgumentOutOfRangeException(nameof(value), "Position is out of bounds.");
            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // Check if the read operation goes beyond the allowed length
        if (_position + count > _length)
            throw new ArgumentOutOfRangeException(nameof(count), "Read operation goes beyond the stream length.");

        var (rangeStart, rangeData) = GetRangeForPosition(_position);

        var rangeOffset = (int)(_position - rangeStart);
        var bytesAvailableInRange = rangeData.Length - rangeOffset;
        if (bytesAvailableInRange < count)
            throw new InvalidOperationException("Attempted to read outside of provided ranges.");

        Array.Copy(rangeData, rangeOffset, buffer, offset, count);
        _position += count;
        return count;
    }

    private Range GetRangeForPosition(long position)
    {
        foreach (var (offset, data) in _dataRanges)
        {
            if (position >= offset && position < offset + data.Length)
                return new(offset, data);
        }

        throw new InvalidOperationException("Attempted to read outside of provided ranges.");
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentException("Invalid seek origin.", nameof(origin)),
        };

        if (newPosition < 0 || newPosition > _length)
            throw new ArgumentOutOfRangeException(nameof(offset), "Seek operation is out of bounds.");

        _position = newPosition;
        return _position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException($"{nameof(SetLength)} is not supported.");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException($"{nameof(Write)} is not supported.");
    }

    public override void Flush()
    {
        // No-op since there's no writable buffer.
    }

    public readonly record struct Range(long Offset, byte[] Data);
}
