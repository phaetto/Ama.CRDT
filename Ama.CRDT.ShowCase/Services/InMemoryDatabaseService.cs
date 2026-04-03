namespace Ama.CRDT.ShowCase.Services;

using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// An implementation of <see cref="IInMemoryDatabaseService"/> using <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// to simulate a thread-safe database for CRDT documents and metadata.
/// </summary>
public sealed class InMemoryDatabaseService([FromKeyedServices("Ama.CRDT")] JsonSerializerOptions jsonOptions) : IInMemoryDatabaseService
{
    private readonly ConcurrentDictionary<string, string> documents = new();
    private readonly ConcurrentDictionary<string, CrdtMetadata> metadata = new();

    public Task<(T document, CrdtMetadata metadata)> GetStateAsync<T>(string key) where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        var typeInfo = jsonOptions.GetTypeInfo(typeof(T));
        var doc = documents.TryGetValue(key, out var json)
            ? (T?)JsonSerializer.Deserialize(json, typeInfo) ?? new T()
            : new T();

        var meta = metadata.TryGetValue(key, out var m) ? m : new CrdtMetadata();

        return Task.FromResult((doc, meta));
    }

    public Task SaveStateAsync<T>(string key, T document, CrdtMetadata metadata) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(metadata);

        var typeInfo = jsonOptions.GetTypeInfo(typeof(T));
        var json = JsonSerializer.Serialize(document, typeInfo);
        
        documents[key] = json;
        this.metadata[key] = metadata;

        return Task.CompletedTask;
    }
}