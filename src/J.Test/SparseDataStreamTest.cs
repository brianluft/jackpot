using J.Core;

namespace J.Test;

[TestClass]
public class SparseDataStreamTest
{
    [TestMethod]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange
        var length = 100L;
        List<SparseDataStream.Range> dataRanges = [new(10, [1, 2, 3]), new(50, [4, 5, 6])];

        // Act
        SparseDataStream stream = new(length, dataRanges);

        // Assert
        Assert.AreEqual(length, stream.Length);
    }

    [TestMethod]
    public void Read_ShouldReturnCorrectDataFromSparseRange()
    {
        // Arrange
        var length = 100L;
        List<SparseDataStream.Range> dataRanges = [new(10, [1, 2, 3]), new(50, [4, 5, 6])];
        SparseDataStream stream = new(length, dataRanges);
        var buffer = new byte[3];

        // Act
        stream.Position = 10;
        var bytesRead = stream.Read(buffer, 0, 3);

        // Assert
        Assert.AreEqual(3, bytesRead);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, buffer);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Read_ShouldThrowExceptionWhenReadingOutsideSparseRange()
    {
        // Arrange
        var length = 100L;
        List<SparseDataStream.Range> dataRanges = [new(10, [1, 2, 3]), new(50, [4, 5, 6])];
        SparseDataStream stream = new(length, dataRanges);
        var buffer = new byte[3];

        // Act
        stream.Position = 5;
        stream.Read(buffer, 0, 3);
    }

    [TestMethod]
    public void Read_ShouldAllowPositionOutsideSparseRangesWithoutException()
    {
        // Arrange
        var length = 100L;
        List<SparseDataStream.Range> dataRanges = [new(10, [1, 2, 3]), new(50, [4, 5, 6])];

        // Act
        SparseDataStream stream =
            new(length, dataRanges)
            {
                Position =
                    7 // Position outside the sparse range but no Read is performed
                ,
            };

        // Assert
        Assert.AreEqual(7, stream.Position);
    }

    [TestMethod]
    public void Read_ShouldSupportMultiRangeReads()
    {
        // Arrange
        var length = 100L;
        List<SparseDataStream.Range> dataRanges = [new(10, [1, 2, 3]), new(50, [4, 5, 6])];
        SparseDataStream stream = new(length, dataRanges);
        var buffer = new byte[6];

        // Act
        stream.Position = 10;
        var bytesRead = stream.Read(buffer, 0, 3); // Read from first range

        stream.Position = 50;
        bytesRead += stream.Read(buffer, 3, 3); // Read from second range

        // Assert
        Assert.AreEqual(6, bytesRead);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6 }, buffer);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Read_ShouldThrowExceptionWhenReadingBeyondStreamLength()
    {
        // Arrange
        var length = 100L;
        List<SparseDataStream.Range> dataRanges = [new(10, [1, 2, 3]), new(50, [4, 5, 6])];
        SparseDataStream stream = new(length, dataRanges);
        var buffer = new byte[4];

        // Act
        stream.Position = 98;
        stream.Read(buffer, 0, 4); // Attempting to read beyond the stream length
    }

    [TestMethod]
    public void CanSeek_ShouldReturnTrue()
    {
        // Arrange
        SparseDataStream stream = new(100, []);

        // Assert
        Assert.IsTrue(stream.CanSeek);
    }

    [TestMethod]
    public void CanRead_ShouldReturnTrue()
    {
        // Arrange
        SparseDataStream stream = new(100, []);

        // Assert
        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public void CanWrite_ShouldReturnFalse()
    {
        // Arrange
        SparseDataStream stream = new(100, []);

        // Assert
        Assert.IsFalse(stream.CanWrite);
    }

    [TestMethod]
    public void Seek_ShouldMovePositionCorrectly()
    {
        // Arrange
        SparseDataStream stream = new(100, []);

        // Act
        stream.Seek(10, SeekOrigin.Begin);

        // Assert
        Assert.AreEqual(10, stream.Position);
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void SetLength_ShouldThrowNotSupportedException()
    {
        // Arrange
        SparseDataStream stream = new(100, []);

        // Act
        stream.SetLength(200); // Not supported for SparseDataStream
    }
}
