namespace Modern.CRDT.Services;

using Modern.CRDT.Models;
using Modern.CRDT.Services.Strategies;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Implements the logic for managing and compacting CRDT metadata.
/// </summary>
public sealed class CrdtMetadataManager(ICrdtStrategyManager strategyManager, ICrdtTimestampProvider timestampProvider) : ICrdtMetadataManager
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc/>
    public void InitializeLwwMetadata<T>(CrdtMetadata metadata, T document) where T : class
    {
        InitializeLwwMetadata(metadata, document, timestampProvider.Now());
    }

    /// <inheritdoc/>
    public void InitializeLwwMetadata<T>(CrdtMetadata metadata, T document, ICrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(timestamp);

        PopulateLwwMetadataRecursive(metadata, document, "$", timestamp);
    }

    /// <inheritdoc/>
    public void PruneLwwTombstones(CrdtMetadata metadata, ICrdtTimestamp threshold)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(threshold);

        var keysToRemove = metadata.Lww
            .Where(kvp => kvp.Value.CompareTo(threshold) < 0)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            metadata.Lww.Remove(key);
        }
    }
    
    /// <inheritdoc/>
    public void AdvanceVersionVector(CrdtMetadata metadata, string replicaId, ICrdtTimestamp newTimestamp)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrEmpty(replicaId);
        ArgumentNullException.ThrowIfNull(newTimestamp);

        metadata.VersionVector[replicaId] = newTimestamp;

        if (metadata.SeenExceptions.Count > 0)
        {
            var exceptionsToRemove = metadata.SeenExceptions
                .Where(op => op.ReplicaId == replicaId && op.Timestamp.CompareTo(newTimestamp) <= 0)
                .ToList();

            foreach (var exception in exceptionsToRemove)
            {
                metadata.SeenExceptions.Remove(exception);
            }
        }
    }

    private void PopulateLwwMetadataRecursive(CrdtMetadata metadata, object obj, string path, ICrdtTimestamp timestamp)
    {
        if (obj is null)
        {
            return;
        }
    
        var type = obj.GetType();
        foreach (var propertyInfo in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!propertyInfo.CanRead || propertyInfo.GetIndexParameters().Length > 0 || propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }
    
            var propertyValue = propertyInfo.GetValue(obj);
            if (propertyValue is null)
            {
                continue;
            }
    
            var jsonPropertyName = DefaultJsonSerializerOptions.PropertyNamingPolicy?.ConvertName(propertyInfo.Name) ?? propertyInfo.Name;
            var propertyPath = path == "$" ? $"$.{jsonPropertyName}" : $"{path}.{jsonPropertyName}";
    
            var strategy = strategyManager.GetStrategy(propertyInfo);
    
            if (strategy is LwwStrategy)
            {
                metadata.Lww[propertyPath] = timestamp;
            }
    
            if (propertyValue is IEnumerable and not string)
            {
                // We don't recurse into collections for LWW metadata initialization.
                // Array strategies manage their own metadata if needed.
                continue;
            }
    
            var propertyType = propertyInfo.PropertyType;
            if (propertyType.IsClass && propertyType != typeof(string))
            {
                PopulateLwwMetadataRecursive(metadata, propertyValue, propertyPath, timestamp);
            }
        }
    }
}