using J.Core.Data;

namespace J.Core;

public sealed class TrackingFileStream(string filePath)
    : FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
{
    private long? _minOffset;
    private long? _maxOffset;

    public OffsetLength? ReadRange =>
        _minOffset.HasValue && _maxOffset.HasValue
            ? new(Offset: _minOffset.Value, Length: (int)(_maxOffset.Value - _minOffset.Value))
            : null;

    public void ClearReadRange()
    {
        _minOffset = null;
        _maxOffset = null;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var start = Position;
        var bytesRead = base.Read(buffer, offset, count);

        if (bytesRead > 0)
        {
            if (!_minOffset.HasValue || start < _minOffset.Value)
                _minOffset = start;

            var end = start + bytesRead;
            if (!_maxOffset.HasValue || end > _maxOffset.Value)
                _maxOffset = end;
        }

        return bytesRead;
    }
}
