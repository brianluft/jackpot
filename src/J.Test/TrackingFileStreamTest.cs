using J.Core;
using J.Test;

namespace J.Test;

[TestClass]
public sealed class TrackingFileStreamTest
{
    private string TestFilename => Path.Combine(TestDir.Path, nameof(TrackingFileStreamTest) + ".tmp");

    // Create a test file with some dummy content
    [TestInitialize]
    public void Setup()
    {
        TestDir.Clear();

        var content = new byte[100];
        for (int i = 0; i < content.Length; i++)
            content[i] = (byte)(i % 256);
        File.WriteAllBytes(TestFilename, content);
    }

    // Delete the test file after tests are complete
    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(TestFilename))
        {
            File.Delete(TestFilename);
        }
    }

    [TestMethod]
    public void ReadRange_ShouldBeNull_Initially()
    {
        // Arrange
        using var stream = new TrackingFileStream(TestFilename);

        // Act
        var readRange = stream.ReadRange;

        // Assert
        Assert.IsNull(readRange, "ReadRange should initially be null.");
    }

    [TestMethod]
    public void ReadRange_ShouldUpdateAfterRead()
    {
        // Arrange
        using var stream = new TrackingFileStream(TestFilename);
        byte[] buffer = new byte[10];

        // Act
        stream.ReadExactly(buffer);

        // Assert
        Assert.IsNotNull(stream.ReadRange, "ReadRange should not be null after a read.");
        Assert.AreEqual(0, stream.ReadRange.Value.Offset, "ReadRange Offset should be 0 after first read.");
        Assert.AreEqual(
            10,
            stream.ReadRange.Value.Length,
            "ReadRange Length should be equal to the number of bytes read."
        );
    }

    [TestMethod]
    public void ReadRange_ShouldTrackMultipleReads()
    {
        // Arrange
        using var stream = new TrackingFileStream(TestFilename);
        byte[] buffer = new byte[10];

        // Act
        stream.ReadExactly(buffer); // Read first 10 bytes
        stream.ReadExactly(buffer); // Read next 10 bytes

        // Assert
        Assert.IsTrue(stream.ReadRange.HasValue);
        Assert.AreEqual(0, stream.ReadRange.Value.Offset, "ReadRange Offset should remain 0 after multiple reads.");
        Assert.AreEqual(20, stream.ReadRange.Value.Length, "ReadRange Length should encompass all reads so far.");
    }

    [TestMethod]
    public void ClearReadRange_ShouldResetReadRange()
    {
        // Arrange
        using var stream = new TrackingFileStream(TestFilename);
        byte[] buffer = new byte[10];

        // Act
        stream.ReadExactly(buffer); // Read 10 bytes
        stream.ClearReadRange();

        // Assert
        Assert.IsNull(stream.ReadRange, "ReadRange should be null after calling ClearReadRange.");
    }

    [TestMethod]
    public void ReadRange_ShouldTrackReadsAfterClear()
    {
        // Arrange
        using var stream = new TrackingFileStream(TestFilename);
        byte[] buffer = new byte[10];

        // Act
        stream.ReadExactly(buffer); // Read 10 bytes
        stream.ClearReadRange(); // Clear the read range
        stream.ReadExactly(buffer); // Read another 10 bytes

        // Assert
        Assert.IsNotNull(stream.ReadRange, "ReadRange should not be null after a read following ClearReadRange.");
        Assert.AreEqual(
            10,
            stream.ReadRange.Value.Offset,
            "ReadRange Offset should match the read position after clearing."
        );
        Assert.AreEqual(
            10,
            stream.ReadRange.Value.Length,
            "ReadRange Length should match the subsequent read length after clearing."
        );
    }
}
