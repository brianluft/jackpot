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
    private const int MAX_THREADS = 16; // empirically determined
    private readonly IAmazonS3 _s3;
    private readonly AsyncRetryPolicy _policy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(10, _ => TimeSpan.FromSeconds(1));
    private readonly Thread[] _threads = new Thread[MAX_THREADS];
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Lock _enqueueLock = new(); // let each file queue all of its tasks together
    private bool _disposedValue;

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
        Task initiateTask,
            completeTask;
        List<Task> partTasks = new(parts.Count);

        lock (_enqueueLock)
        {
            initiateTask = Queue(() =>
            {
                InitiateMultipartUpload(fileState, cancel);
            });

            var partsComplete = 0; // interlocked

            foreach (var part in parts)
            {
                partTasks.Add(
                    Queue(() =>
                    {
                        initiateTask.Wait(cancel);
                        if (initiateTask.IsFaulted)
                            return;

                        var etag = UploadPart(fileState, part, cancel);
                        fileState.PartETags.Add(new(part.PartNumber, etag));

                        var i = Interlocked.Increment(ref partsComplete);
                        updateProgress(i / (double)parts.Count);
                    })
                );
            }

            completeTask = Queue(() =>
            {
                foreach (var t in partTasks)
                {
                    t.Wait(cancel);
                    if (t.IsFaulted)
                        return;
                }

                CompleteMultipartUpload(fileState, cancel);
            });
        }

        completeTask.Wait(cancel);

        initiateTask.GetAwaiter().GetResult();
        foreach (var t in partTasks)
            t.GetAwaiter().GetResult();
        completeTask.GetAwaiter().GetResult();
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
                            },
                            cancel
                        )
                        .ConfigureAwait(false);

                    return response.ETag;
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
