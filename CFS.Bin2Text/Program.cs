using System.Collections.Concurrent;

const int CHUNK_SIZE = 1024 << 10;

using var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

using var inputFile = File.OpenRead(args[0]);
using var outputFile = File.CreateText(args[1]);

var binaryBlockQueue = new ConcurrentQueue<byte[]>();
var textBlockQueue = new ConcurrentQueue<string>();

Parallel.Invoke(
    () =>
    {
        var block = new byte[CHUNK_SIZE];
        int readCount = inputFile.Read(block, 0, block.Length);
        while (readCount > 0)
        {
            binaryBlockQueue.Enqueue(block[..(readCount - 1)]);
            readCount = inputFile.Read(block, 0, block.Length);

            SpinWait.SpinUntil(() => binaryBlockQueue.Count < 1024);
        }

        cancellationTokenSource.Cancel();
    },
    () =>
    {
        while (true)
        {
            if (binaryBlockQueue.TryDequeue(out byte[]? block))
            {
                _ = Task.Run(() =>
                {
                    var textBlock = Convert.ToBase64String(block);
                    textBlockQueue.Enqueue(textBlock);
                });
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(25);
            }
            else 
            {
                break;
            }
        }
    },
    () =>
    {
        while (true)
        {
            if (textBlockQueue.TryDequeue(out string? textBlock))
            {
                outputFile.WriteLine(textBlock);
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(25);
            }
            else 
            {
                break;
            }
        }
    }
    );