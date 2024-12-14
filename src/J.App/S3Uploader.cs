using System.Collections.Concurrent;
using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using J.Core;
using J.Core.Data;
using Polly;
using Polly.Retry;

namespace J.App;

public sealed class S3Uploader : IDisposable
{
    private const int PART_SIZE = 10_000_000; // B2's minimum part size is 5 MB, S3's is 5 MiB (!)
    private const int MAX_THREADS = 8; // empirically determined
    private readonly IAmazonS3 _s3;
    private readonly AsyncRetryPolicy _policy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(10, _ => TimeSpan.FromSeconds(1));
    private readonly Thread[] _threads = new Thread[MAX_THREADS];
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Lock _enqueueLock = new(); // let each file queue all of its tasks together
    private bool _disposedValue;

    // interlocked, on retry we subtract the bytes that were uploaded but are now thrown out.
    private long _uploadedBytesWithRollbacks = 0;
    public long UploadedBytesWithRollbacks => Interlocked.Read(ref _uploadedBytesWithRollbacks);

    // interlocked, on retry we leave it alone and add even more on for subsequent attempts.
    private long _uploadedBytesMonotonic = 0;
    public long UploadedBytesMonotonic => Interlocked.Read(ref _uploadedBytesMonotonic);

    public S3Uploader(AccountSettingsProvider accountSettingsProvider)
    {
        _s3 = accountSettingsProvider.CreateAmazonS3Client();

        for (var i = 0; i < MAX_THREADS; i++)
        {
            Thread thread = new(WorkerThread);
            thread.Start();
            _threads[i] = thread;
        }
    }

    public void Reset()
    {
        _ = Interlocked.Exchange(ref _uploadedBytesWithRollbacks, 0);
        _ = Interlocked.Exchange(ref _uploadedBytesMonotonic, 0);
    }

    private void WorkerThread()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            action();
        }
    }

    private Task Queue(Action action)
    {
        TaskCompletionSource tcs = new();
        _queue.Add(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private sealed class FileState(string filePath, string bucket, string key)
    {
        public string FilePath { get; } = filePath;
        public string Bucket { get; } = bucket;
        public string Key { get; } = key;
        public string? UploadId { get; set; } = null;
        public ConcurrentBag<PartETag> PartETags { get; } = [];
    }

    public void PutObject(
        string filePath,
        string bucket,
        string key,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        var parts = PlanParts(filePath).ToList();

        FileState fileState = new(filePath, bucket, key);
        List<Task> partTasks = new(parts.Count);

        InitiateMultipartUpload(fileState, cancel);
        var completePartsBytes = 0L; // interlocked
        try
        {
            var completeParts = 0; // interlocked

            // If one part fails, then cancel every other part.
            using var fileCts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            var fileCancel = fileCts.Token;

            lock (_enqueueLock)
            {
                foreach (var part in parts)
                {
                    partTasks.Add(
                        Queue(() =>
                        {
                            try
                            {
                                if (!fileCancel.IsCancellationRequested)
                                {
                                    var etag = UploadPart(fileState, part, fileCancel);
                                    fileState.PartETags.Add(new(part.PartNumber, etag));

                                    Interlocked.Add(ref completePartsBytes, part.OffsetLength.Length);
                                    var i = Interlocked.Increment(ref completeParts);
                                    updateProgress(i / (double)parts.Count);
                                }
                            }
                            catch
                            {
                                fileCts.Cancel();
                                throw;
                            }
                        })
                    );
                }
            }

            foreach (var t in partTasks)
            {
                t.Wait(cancel);
                cancel.ThrowIfCancellationRequested();
                t.GetAwaiter().GetResult();
            }

            CompleteMultipartUpload(fileState, cancel);
        }
        catch
        {
            Interlocked.Add(ref _uploadedBytesWithRollbacks, -completePartsBytes);

            try
            {
                AbortMultipartUpload(fileState, cancel);
            }
            catch
            {
                // If the abort also fails, we want to throw the original exception.
            }

            throw;
        }
    }

    private static IEnumerable<Part> PlanParts(string filePath)
    {
        var fileSize = new FileInfo(filePath).Length;
        var offset = 0L;
        var partNumber = 1;

        while (fileSize > 0)
        {
            var length = (int)Math.Min(PART_SIZE, fileSize);
            if (partNumber >= 10_000)
                throw new Exception("File is too big!");
            yield return new(partNumber, new(offset, length));
            offset += length;
            fileSize -= length;
            partNumber++;
        }
    }

    private void InitiateMultipartUpload(FileState fileState, CancellationToken cancel)
    {
        _policy
            .ExecuteAsync(
                async cancel =>
                {
                    var response = await _s3.InitiateMultipartUploadAsync(fileState.Bucket, fileState.Key, cancel)
                        .ConfigureAwait(false);
                    fileState.UploadId = response.UploadId;
                },
                cancel
            )
            .GetAwaiter()
            .GetResult();
    }

    private string UploadPart(FileState fileState, Part part, CancellationToken cancel)
    {
        Debug.Assert(fileState.UploadId is not null);

        return _policy
            .ExecuteAsync(
                async cancel =>
                {
                    long uploadedThisAttempt = 0;
                    try
                    {
                        var response = await _s3.UploadPartAsync(
                                new()
                                {
                                    BucketName = fileState.Bucket,
                                    FilePath = fileState.FilePath,
                                    FilePosition = part.OffsetLength.Offset,
                                    Key = fileState.Key,
                                    PartSize = part.OffsetLength.Length,
                                    PartNumber = part.PartNumber,
                                    UploadId = fileState.UploadId,
                                    StreamTransferProgress = (sender, e) =>
                                    {
                                        uploadedThisAttempt += e.IncrementTransferred;
                                        Interlocked.Add(ref _uploadedBytesMonotonic, e.IncrementTransferred);
                                        Interlocked.Add(ref _uploadedBytesWithRollbacks, e.IncrementTransferred);
                                    },
                                },
                                cancel
                            )
                            .ConfigureAwait(false);

                        // This should be zero, right? Just in case.
                        var slack = part.OffsetLength.Length - uploadedThisAttempt;
                        Interlocked.Add(ref _uploadedBytesMonotonic, slack);
                        Interlocked.Add(ref _uploadedBytesWithRollbacks, slack);

                        return response.ETag;
                    }
                    catch
                    {
                        Interlocked.Add(ref _uploadedBytesWithRollbacks, -uploadedThisAttempt);
                        throw;
                    }
                },
                cancel
            )
            .GetAwaiter()
            .GetResult();
    }

    private void CompleteMultipartUpload(FileState fileState, CancellationToken cancel)
    {
        _policy
            .ExecuteAsync(
                async cancel =>
                {
                    await _s3.CompleteMultipartUploadAsync(
                            new()
                            {
                                BucketName = fileState.Bucket,
                                Key = fileState.Key,
                                UploadId = fileState.UploadId,
                                PartETags = [.. fileState.PartETags.OrderBy(x => x.PartNumber)],
                            },
                            cancel
                        )
                        .ConfigureAwait(false);
                },
                cancel
            )
            .GetAwaiter()
            .GetResult();
    }

    private void AbortMultipartUpload(FileState fileState, CancellationToken cancel)
    {
        if (fileState.UploadId is null)
            return;

        _policy
            .ExecuteAsync(
                async cancel =>
                {
                    await _s3.AbortMultipartUploadAsync(
                            new()
                            {
                                BucketName = fileState.Bucket,
                                Key = fileState.Key,
                                UploadId = fileState.UploadId,
                            },
                            cancel
                        )
                        .ConfigureAwait(false);
                },
                cancel
            )
            .GetAwaiter()
            .GetResult();
    }

    private readonly record struct Part(int PartNumber, OffsetLength OffsetLength);

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _queue.CompleteAdding();
            foreach (var t in _threads)
                t?.Join();
            _queue.Dispose();

            if (disposing)
            {
                // dispose managed state (managed objects)
                _s3.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null

            _disposedValue = true;
        }
    }

    // override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~S3Uploader()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
