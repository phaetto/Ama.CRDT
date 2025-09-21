namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

public sealed partial class FileSystemPartitionStreamProvider(ReplicaContext replicaContext) : IPartitionStreamProvider, IDisposable
{
    private readonly string replicaBasePath = Path.Combine(Environment.CurrentDirectory, "data", replicaContext.ReplicaId);
    private readonly ConcurrentDictionary<string, Stream> openStreams = new();

    [GeneratedRegex("[^a-zA-Z0-9_.-]+", RegexOptions.Compiled)]
    private static partial Regex SanitizePathRegex();

    public string GetReplicaBasePath() => replicaBasePath;
    
    public Task<Stream> GetPropertyIndexStreamAsync(string propertyName)
    {
        var sanitizedPropertyName = SanitizePathRegex().Replace(propertyName, "_");
        var indexPath = Path.Combine(replicaBasePath, $"index_{sanitizedPropertyName}.bin");
        return GetOrCreateStreamAsync(indexPath);
    }

    public Task<Stream> GetPropertyDataStreamAsync(IComparable logicalKey, string propertyName)
    {
        var sanitizedPropertyName = SanitizePathRegex().Replace(propertyName, "_");
        var dataPath = Path.Combine(replicaBasePath, "data", $"{logicalKey}_{sanitizedPropertyName}.dat");
        return GetOrCreateStreamAsync(dataPath);
    }
    
    public Task<Stream> GetHeaderIndexStreamAsync()
    {
        var indexPath = Path.Combine(replicaBasePath, "index_header.bin");
        return GetOrCreateStreamAsync(indexPath);
    }

    public Task<Stream> GetHeaderDataStreamAsync(IComparable logicalKey)
    {
        var dataPath = Path.Combine(replicaBasePath, "data", $"{logicalKey}_header.dat");
        return GetOrCreateStreamAsync(dataPath);
    }
    
    private Task<Stream> GetOrCreateStreamAsync(string path)
    {
        var stream = openStreams.GetOrAdd(path, p =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            return new FileStream(p, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
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