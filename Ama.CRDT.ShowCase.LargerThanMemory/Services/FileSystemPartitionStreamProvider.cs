namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using Ama.CRDT.Partitioning.Streams.Services;
using Ama.CRDT.Services;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class FileSystemPartitionStreamProvider : IPartitionStreamProvider, IDisposable
{
    private readonly string replicaBasePath;
    private readonly ConcurrentDictionary<string, Stream> openStreams = new();

    public FileSystemPartitionStreamProvider(ReplicaContext replicaContext)
    {
        ArgumentNullException.ThrowIfNull(replicaContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaContext.ReplicaId);
        
        replicaBasePath = Path.Combine(Environment.CurrentDirectory, "data", replicaContext.ReplicaId);
    }

    [GeneratedRegex("[^a-zA-Z0-9_.-]+", RegexOptions.Compiled)]
    private static partial Regex SanitizePathRegex();

    public string GetReplicaBasePath() => replicaBasePath;
    
    public Task<Stream> GetPropertyIndexStreamAsync(string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var sanitizedPropertyName = SanitizePathRegex().Replace(propertyName, "_");
        var indexPath = Path.Combine(replicaBasePath, $"index_{sanitizedPropertyName}.bin");
        return GetOrCreateStreamAsync(indexPath, cancellationToken);
    }

    public Task<Stream> GetPropertyDataStreamAsync(IComparable logicalKey, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var sanitizedPropertyName = SanitizePathRegex().Replace(propertyName, "_");
        var dataPath = Path.Combine(replicaBasePath, "data", $"{logicalKey}_{sanitizedPropertyName}.dat");
        return GetOrCreateStreamAsync(dataPath, cancellationToken);
    }
    
    public Task<Stream> GetHeaderIndexStreamAsync(CancellationToken cancellationToken = default)
    {
        var indexPath = Path.Combine(replicaBasePath, "index_header.bin");
        return GetOrCreateStreamAsync(indexPath, cancellationToken);
    }

    public Task<Stream> GetHeaderDataStreamAsync(IComparable logicalKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logicalKey);

        var dataPath = Path.Combine(replicaBasePath, "data", $"{logicalKey}_header.dat");
        return GetOrCreateStreamAsync(dataPath, cancellationToken);
    }
    
    private Task<Stream> GetOrCreateStreamAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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