namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using System.Collections.Concurrent;

public sealed class FileSystemPartitionStreamProvider(ReplicaContext replicaContext) : IPartitionStreamProvider, IDisposable
{
    private readonly string replicaBasePath = Path.Combine(Environment.CurrentDirectory, "data", replicaContext.ReplicaId);
    private readonly ConcurrentDictionary<string, Stream> openStreams = new();

    public string GetReplicaBasePath() => replicaBasePath;

    public Task<Stream> GetIndexStreamAsync()
    {
        var indexPath = Path.Combine(replicaBasePath, "index.bin");
        var stream = openStreams.GetOrAdd(indexPath, path =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        });
        return Task.FromResult(stream);
    }

    public Task<Stream> GetDataStreamAsync(object logicalKey)
    {
        var dataPath = Path.Combine(replicaBasePath, "data", $"{logicalKey}.dat");
        var stream = openStreams.GetOrAdd(dataPath, path =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        });
        return Task.FromResult(stream);
    }
    
    public void Dispose()
    {
        foreach (var stream in openStreams.Values)
        {
            stream.Dispose();
        }
    }
}