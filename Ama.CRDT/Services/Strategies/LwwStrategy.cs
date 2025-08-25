namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Microsoft.Extensions.Options;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Ama.CRDT.Attributes.Strategies;

/// <summary>
/// Implements the Last-Writer-Wins (LWW) strategy.
/// </summary>
[Commutative]
[Associative]
[Idempotent]
public sealed class LwwStrategy : ICrdtStrategy
{
    private readonly string replicaId;

    /// <summary>
    /// Initializes a new instance of the <see cref="LwwStrategy"/> class.
    /// </summary>
    /// <param name="options">Configuration options containing the replica ID.</param>
    public LwwStrategy(IOptions<CrdtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        replicaId = options.Value.ReplicaId;
    }

    /// <inheritdoc/>
    public void GeneratePatch(ICrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, object? originalValue, object? modifiedValue, CrdtMetadata originalMeta, CrdtMetadata modifiedMeta)
    {
        var propertyType = property.PropertyType;
        if (propertyType.IsClass && propertyType != typeof(string) && !CrdtPatcher.IsCollection(propertyType))
        {
            patcher.DifferentiateObject(path, property.PropertyType, originalValue, originalMeta, modifiedValue, modifiedMeta, operations);
            return;
        }

        if (Equals(originalValue, modifiedValue))
        {
            return;
        }
        
        modifiedMeta.Lww.TryGetValue(path, out var modifiedTimestamp);
        originalMeta.Lww.TryGetValue(path, out var originalTimestamp);
        
        if (modifiedTimestamp is null || originalTimestamp is not null && modifiedTimestamp.CompareTo(originalTimestamp) <= 0)
        {
            return;
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Upsert, modifiedValue, modifiedTimestamp);
        
        if (modifiedValue is null)
        {
            operation = operation with { Type = OperationType.Remove, Value = null };
        }
        
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(metadata);

        metadata.Lww.TryGetValue(operation.JsonPath, out var lwwTs);
        if (lwwTs is not null && operation.Timestamp.CompareTo(lwwTs) <= 0)
        {
            return;
        }

        var (parent, property, finalSegment) = PocoPathHelper.ResolvePath(root, operation.JsonPath);

        if (parent is null) return;

        if (property is null)
        {
            if (finalSegment is string propertyName)
            {
                property = parent.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            }
        }

        if (property is null || !property.CanWrite) return;
        
        if (operation.Type == OperationType.Remove)
        {
            property.SetValue(parent, null);
            metadata.Lww[operation.JsonPath] = operation.Timestamp;
        }
        else if (operation.Type == OperationType.Upsert)
        {
            var value = DeserializeValue(operation.Value, property.PropertyType);
            property.SetValue(parent, value);
            metadata.Lww[operation.JsonPath] = operation.Timestamp;
        }
    }

    private static object? DeserializeValue(object? value, Type targetType)
    {
        if (value is null) return null;
        if (targetType.IsInstanceOfType(value)) return value;

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is IConvertible)
        {
            try
            {
                return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException)
            {
                // Fall through to dictionary mapping logic.
            }
        }

        if (value is IDictionary<string, object> dictionary && !underlyingType.IsPrimitive && underlyingType != typeof(string))
        {
            var instance = Activator.CreateInstance(underlyingType);
            if (instance is null) return null;

            var properties = underlyingType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite);

            foreach (var property in properties)
            {
                var key = dictionary.Keys.FirstOrDefault(k => string.Equals(k, property.Name, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    var propValue = dictionary[key];
                    property.SetValue(instance, DeserializeValue(propValue, property.PropertyType));
                }
            }
            return instance;
        }

        throw new InvalidOperationException($"Failed to convert value of type '{value.GetType().Name}' to '{targetType.Name}'.");
    }
}