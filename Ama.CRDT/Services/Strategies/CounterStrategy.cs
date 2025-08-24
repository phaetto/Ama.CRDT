namespace Ama.CRDT.Services.Strategies;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Microsoft.Extensions.Options;
using Ama.CRDT.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

/// <summary>
/// A CRDT strategy for handling numeric properties as counters.
/// It generates 'Increment' operations based on the delta between two values
/// and applies these operations by adding the delta to the current value.
/// </summary>
public sealed class CounterStrategy : ICrdtStrategy
{
    private readonly ICrdtTimestampProvider timestampProvider;
    private readonly string replicaId;

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterStrategy"/> class.
    /// </summary>
    /// <param name="timestampProvider">The provider for generating timestamps.</param>
    /// <param name="options">Configuration options containing the replica ID.</param>
    public CounterStrategy(ICrdtTimestampProvider timestampProvider, IOptions<CrdtOptions> options)
    {
        this.timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));
        ArgumentNullException.ThrowIfNull(options?.Value);
        replicaId = options.Value.ReplicaId;
    }

    /// <inheritdoc/>
    public void GeneratePatch(ICrdtPatcher patcher, List<CrdtOperation> operations, string path, PropertyInfo property, object? originalValue, object? modifiedValue, CrdtMetadata originalMeta, CrdtMetadata modifiedMeta)
    {
        var originalNumeric = Convert.ToDecimal(originalValue ?? 0);
        var modifiedNumeric = Convert.ToDecimal(modifiedValue ?? 0);

        var delta = modifiedNumeric - originalNumeric;

        if (delta == 0)
        {
            return;
        }
        
        modifiedMeta.Lww.TryGetValue(path, out var timestamp);
        if (timestamp is null)
        {
            timestamp = timestampProvider.Now();
        }

        var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, delta, timestamp);
        operations.Add(operation);
    }

    /// <inheritdoc/>
    public void ApplyOperation(object root, CrdtOperation operation)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (operation.Type != OperationType.Increment)
        {
            throw new InvalidOperationException($"CounterStrategy can only handle 'Increment' operations, but received '{operation.Type}'.");
        }
        
        var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);

        if (parent is null || property is null || !property.CanWrite) return;
        
        var incrementValue = Convert.ToDecimal(operation.Value ?? 0, CultureInfo.InvariantCulture);
        
        var existingValue = property.GetValue(parent) ?? 0;
        var existingNumeric = Convert.ToDecimal(existingValue, CultureInfo.InvariantCulture);
        
        var newValue = existingNumeric + incrementValue;
        
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var convertedNewValue = Convert.ChangeType(newValue, targetType, CultureInfo.InvariantCulture);

        property.SetValue(parent, convertedNewValue);
    }
}