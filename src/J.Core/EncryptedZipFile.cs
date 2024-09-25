using System.Text;
using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip;
using J.Core.Data;

namespace J.Core;

public static class EncryptedZipFile
{
    public static void CreateLibraryZip(Stream outStream, string entryName, Stream entryStream, Password password)
    {
        using ZipOutputStream zipStream = new(outStream) { Password = password.Value, IsStreamOwner = false };
        AddZipEntry(zipStream, entryStream, entryName, DateTime.Now, CompressionMethod.Deflated);
    }

    public static void CreateMovieZip(string zipFilePath, string dir, Password password, out ZipIndex zipIndex)
    {
        using (var fileStream = File.Create(zipFilePath))
        {
            using ZipOutputStream zipStream = new(fileStream) { Password = password.Value };
            foreach (var filePath in Directory.GetFiles(dir))
            {
                using var entryStream = File.OpenRead(filePath);
                AddZipEntry(
                    zipStream,
                    entryStream,
                    entryName: Path.GetFileName(filePath),
                    entryTime: new FileInfo(filePath).LastWriteTime
                );
            }
        }

        zipIndex = GetZipIndex(zipFilePath, password);
        AppendZipIndex(zipFilePath, zipIndex);
    }

    private static void AddZipEntry(
        ZipOutputStream zipStream,
        Stream entryStream,
        string entryName,
        DateTime entryTime,
        CompressionMethod? method = null
    )
    {
        ZipEntry entry =
            new(entryName)
            {
                DateTime = entryTime,
                AESKeySize = 256,
                CompressionMethod = method ?? CompressionMethod.Stored,
            };

        zipStream.PutNextEntry(entry);

        var buffer = new byte[65536];
        int bytesRead;
        while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            zipStream.Write(buffer, 0, bytesRead);
        }

        zipStream.CloseEntry();
    }

    public static void ExtractSingleFile(Stream inStream, string entryName, Stream outStream, Password password)
    {
        using ZipFile zipFile = new(inStream);
        zipFile.Password = password.Value;
        var entry = zipFile.GetEntry(entryName);
        using var entryStream = zipFile.GetInputStream(entry);
        entryStream.CopyTo(outStream);
    }

    public static void ExtractToDirectory(string zipFilePath, string outputDirectory, Password password)
    {
        if (string.IsNullOrEmpty(zipFilePath) || !File.Exists(zipFilePath))
            throw new ArgumentException("The ZIP file path does not exist.");

        if (string.IsNullOrEmpty(outputDirectory))
            throw new ArgumentException("The output directory must be specified.");

        Directory.CreateDirectory(outputDirectory);

        using ZipFile zipFile = new(zipFilePath);
        zipFile.Password = password.Value;

        foreach (ZipEntry entry in zipFile)
        {
            string filePath = Path.Combine(outputDirectory, entry.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using (Stream entryStream = zipFile.GetInputStream(entry))
            {
                using FileStream fileStream = File.Create(filePath);
                entryStream.CopyTo(fileStream);
            }

            File.SetLastWriteTime(filePath, entry.DateTime);
        }
    }

    private static ZipIndex GetZipIndex(string zipFilePath, Password password)
    {
        List<ZipEntryLocation> entryLocations = [];

        using TrackingFileStream fileStream = new(zipFilePath);
        using ZipFile zipFile = new(fileStream);
        zipFile.Password = password.Value;

        foreach (ZipEntry entry in zipFile)
        {
            // Do nothing; we're just reading the table of contents.
            _ = zipFile.GetEntry(entry.Name);
        }

        if (!fileStream.ReadRange.HasValue)
            throw new Exception("Expected to see some reads but none were issued.");

        var (zipHeaderOffset, _) = fileStream.ReadRange.Value;

        // I don't know why this slop 1000 bytes is necessary.
        zipHeaderOffset = Math.Max(0, zipHeaderOffset - 1000);
        var zipHeaderLength = (int)(new FileInfo(zipFilePath).Length - zipHeaderOffset);

        OffsetLength zipHeaderRange = new(zipHeaderOffset, zipHeaderLength);

        foreach (ZipEntry entry in zipFile)
        {
            if (!entry.IsFile)
                continue;

            fileStream.ClearReadRange();

            // Read the entire entry to ensure ranges are recorded.
            using Stream entryStream = zipFile.GetInputStream(entry);
            entryStream.CopyTo(Stream.Null);

            if (!fileStream.ReadRange.HasValue)
                throw new Exception("Expected to see some reads but none were issued.");

            var (offset, length) = fileStream.ReadRange.Value;

            // I don't know why *this* slop 1000 bytes is necessary, either.
            if (offset < 1000)
            {
                length += (int)offset;
                offset = 0;
            }
            else
            {
                offset -= 1000;
                length += 1000;
            }

            entryLocations.Add(new(entry.Name, new(offset, length)));
        }

        return new(entryLocations, zipHeaderRange);
    }

    private static void AppendZipIndex(string zipFilePath, ZipIndex zipIndex)
    {
        var jsonString = JsonSerializer.Serialize(zipIndex);
        UTF8Encoding utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
        var jsonBytes = utf8WithoutBom.GetBytes(jsonString);

        using var fileStream = File.Open(zipFilePath, FileMode.Append, FileAccess.Write, FileShare.None);
        var start = fileStream.Position;
        fileStream.Write(jsonBytes);
        fileStream.Write(BitConverter.GetBytes((long)start));
    }

    public static void ReadEntry(Stream input, Stream output, string entryName, Password password)
    {
        using ZipFile zipFile = new(input);
        zipFile.Password = password.Value;
        foreach (ZipEntry entry in zipFile)
        {
            if (entry.Name == entryName)
            {
                using Stream entryStream = zipFile.GetInputStream(entry);

                // Don't use CopyToAsync, to work around this bug:
                // https://github.com/icsharpcode/SharpZipLib/issues/823
                entryStream.CopyTo(output);
                return;
            }
        }

        throw new ArgumentException("Entry not found.");
    }
}
