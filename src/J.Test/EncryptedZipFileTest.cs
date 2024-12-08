using System.Text;
using J.Core;
using J.Core.Data;

namespace J.Test;

[TestClass]
public sealed class EncryptedZipFileTest
{
    [TestMethod]
    public void TextCreateAndExtract()
    {
        TestDir.Clear();
        var dir1 = Path.Combine(TestDir.Path, "dir1");
        Directory.CreateDirectory(dir1);

        var fileString1 = "this is a test!";
        var fileBytes1 = Encoding.ASCII.GetBytes(fileString1);
        var filename1 = "file1.bin";
        var filePath1 = Path.Combine(dir1, filename1);
        File.WriteAllBytes(filePath1, fileBytes1);

        var fileString2 = "another test!";
        var fileBytes2 = Encoding.ASCII.GetBytes(fileString2);
        var filename2 = "file2.bin";
        var filePath2 = Path.Combine(dir1, filename2);
        File.WriteAllBytes(filePath2, fileBytes2);

        var zipFilePath = Path.Combine(TestDir.Path, "out.zip");
        Password password = new("foobar");
        ImportProgress importProgress = new(_ => { }, _ => { });
        EncryptedZipFile.CreateMovieZip(zipFilePath, dir1, password, importProgress, out _, default);

        var dir2 = Path.Combine(TestDir.Path, "dir2");
        Directory.CreateDirectory(dir2);
        EncryptedZipFile.ExtractToDirectory(zipFilePath, dir2, password);

        var extractedFilePath1 = Path.Combine(dir2, filename1);
        var extractedFileString1 = File.ReadAllText(extractedFilePath1);
        Assert.AreEqual(fileString1, extractedFileString1);

        var extractedFilePath2 = Path.Combine(dir2, filename2);
        var extractedFileString2 = File.ReadAllText(extractedFilePath2);
        Assert.AreEqual(fileString2, extractedFileString2);
    }

    [TestMethod]
    public void TestSparseUsage()
    {
        TestDir.Clear();
        var dir = Path.Combine(TestDir.Path, "dir");
        Directory.CreateDirectory(dir);

        // Generate a series of files, file0.bin through file9.bin.
        // Each contains 1MB of random bytes.
        for (int i = 0; i < 10; i++)
        {
            var filePath = Path.Combine(dir, $"file{i}.bin");
            var fileBytes = new byte[1024 * 1024];
            new Random().NextBytes(fileBytes);
            File.WriteAllBytes(filePath, fileBytes);
        }

        // Create a zip
        var zipFilePath = Path.Combine(TestDir.Path, "out.zip");
        Password password = new("foobar");
        ImportProgress importProgress = new(_ => { }, _ => { });
        EncryptedZipFile.CreateMovieZip(zipFilePath, dir, password, importProgress, out var zipIndex, default);

        // Try extracting file0.bin using a sparse stream.
        var zipHeaderData = ReadByteRange(zipFilePath, zipIndex.ZipHeader);
        var zipIndexEntry = zipIndex.Entries.Single(x => x.Name == "file0.bin");
        var entryData = ReadByteRange(zipFilePath, zipIndexEntry.OffsetLength);
        List<SparseDataStream.Range> ranges =
        [
            new(0, [0x50, 0x4B, 0x03, 0x04]),
            new(zipIndex.ZipHeader.Offset, zipHeaderData),
            new(zipIndexEntry.OffsetLength.Offset, entryData),
        ];
        using var sparseStream = new SparseDataStream(zipIndex.ZipHeader.Offset + zipHeaderData.Length, ranges);
        var extractedFilePath = Path.Combine(TestDir.Path, "extracted");
        using (var fs = File.Create(extractedFilePath))
        {
            EncryptedZipFile.ReadEntry(sparseStream, fs, "file0.bin", password);
        }

        // Verify the extracted file.
        var extractedFileBytes = File.ReadAllBytes(extractedFilePath);
        var originalFileBytes = File.ReadAllBytes(Path.Combine(dir, "file0.bin"));
        CollectionAssert.AreEqual(originalFileBytes, extractedFileBytes);
    }

    private static byte[] ReadByteRange(string filePath, OffsetLength offsetLength)
    {
        using var fs = File.OpenRead(filePath);
        fs.Seek(offsetLength.Offset, SeekOrigin.Begin);
        var buffer = new byte[offsetLength.Length];
        fs.ReadExactly(buffer);
        return buffer;
    }
}
