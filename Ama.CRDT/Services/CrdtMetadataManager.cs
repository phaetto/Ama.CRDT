namespace Ama.CRDT.Services;

using Ama.CRDT.Models;

using Ama.CRDT.Services.Strategies;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <inheritdoc/>
public sealed class CrdtMetadataManager(ICrdtStrategyManager strategyManager, ICrdtTimestampProvider timestampProvider) : ICrdtMetadataManager
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    
    /// <inheritdoc/>
    public CrdtMetadata Initialize<T>([DisallowNull] T document) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        return Initialize(document, timestampProvider.Now());
    }

    /// <inheritdoc/>
    public CrdtMetadata Initialize<T>([DisallowNull] T document, [DisallowNull] ICrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(timestamp);

        var metadata = new CrdtMetadata();
        PopulateMetadataRecursive(metadata, document, "$", timestamp);
        return metadata;
    }

    /// <inheritdoc/>
    public void InitializeLwwMetadata<T>([DisallowNull] CrdtMetadata metadata, [DisallowNull] T document) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);
        InitializeLwwMetadata(metadata, document, timestampProvider.Now());
    }

    /// <inheritdoc/>
    public void InitializeLwwMetadata<T>([DisallowNull] CrdtMetadata metadata, [DisallowNull] T document, [DisallowNull] ICrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(timestamp);

        PopulateMetadataRecursive(metadata, document, "$", timestamp);
    }

    /// <inheritdoc/>
    public void Reset<T>([DisallowNull] CrdtMetadata metadata, [DisallowNull] T document) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);
        Reset(metadata, document, timestampProvider.Now());
    }

    /// <inheritdoc/>
    public void Reset<T>([DisallowNull] CrdtMetadata metadata, [DisallowNull] T document, [DisallowNull] ICrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(timestamp);

        metadata.Lww.Clear();
        metadata.PositionalTrackers.Clear();

        PopulateMetadataRecursive(metadata, document, "$", timestamp);
    }



    /// <inheritdoc/>
    public void PruneLwwTombstones([DisallowNull] CrdtMetadata metadata, [DisallowNull] ICrdtTimestamp threshold)
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
    public void AdvanceVersionVector([DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        AdvanceVersionVector(metadata, operation.ReplicaId, operation.Timestamp);
    }

    /// <inheritdoc/>
    public void AdvanceVersionVector([DisallowNull] CrdtMetadata metadata, string replicaId, [DisallowNull] ICrdtTimestamp timestamp)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(timestamp);

        if (string.IsNullOrWhiteSpace(replicaId))
        {
            throw new ArgumentException("Replica ID cannot be null or whitespace.", nameof(replicaId));
        }

        if (metadata.VersionVector.TryGetValue(replicaId, out var currentTimestamp) && timestamp.CompareTo(currentTimestamp) <= 0)
        {
            return;
        }

        metadata.VersionVector[replicaId] = timestamp;

        if (metadata.SeenExceptions.Count > 0)
        {
            var exceptionsToRemove = metadata.SeenExceptions
                .Where(op => op.ReplicaId == replicaId && op.Timestamp.CompareTo(timestamp) <= 0)
                .ToList();

            foreach (var exception in exceptionsToRemove)
            {
                metadata.SeenExceptions.Remove(exception);
            }
        }
    }

    private void PopulateMetadataRecursive(CrdtMetadata metadata, object obj, string path, ICrdtTimestamp timestamp)
    {
        if (obj is null)
        {
            return;
        }

        if (obj is IEnumerable collection && obj is not string)
        {
            var i = 0;
            foreach (var item in collection)
            {
                if (item is not null)
                {
                    var itemType = item.GetType();
                    if (itemType.IsClass && itemType != typeof(string))
                    {
                        PopulateMetadataRecursive(metadata, item, $"{path}[{i}]", timestamp);
                    }
                }
                i++;
            }
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
            else if (strategy is ArrayLcsStrategy)
            {
                if (propertyValue is IList list)
                {
                    if (list.Count > 0)
                    {
                        var tracker = new List<PositionalIdentifier>(list.Count);
                        for (var i = 0; i < list.Count; i++)
                        {
                            tracker.Add(new PositionalIdentifier((i + 1).ToString(), Guid.Empty));
                        }
                        metadata.PositionalTrackers[propertyPath] = tracker;
                    }
                    else
                    {
                        metadata.PositionalTrackers.Remove(propertyPath);
                    }
                }
            }
            
            var propertyType = propertyInfo.PropertyType;
            if ((propertyValue is IEnumerable and not string) || (propertyType.IsClass && propertyType != typeof(string)))
            {
                PopulateMetadataRecursive(metadata, propertyValue, propertyPath, timestamp);
            }
        }
    }
}