namespace Ama.CRDT.Services.Strategies;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Microsoft.Extensions.Options;

/// <summary>
/// Implements the G-Counter (Grow-Only Counter) strategy. This counter only supports positive increments.
/// </summary>
[Commutative]
[Associative]
[IdempotentShortTermImplementation]
[Mergeable]
public sealed class GCounterStrategy(ICrdtTimestampProvider timestampProvider, IOptions<CrdtOptions> options) : ICrdtStrategy
{
    private readonly string replicaId = options.Value.ReplicaId;

    /// <inheritdoc/>
    public void GeneratePatch([DisallowNull] ICrdtPatcher patcher, [DisallowNull] List<CrdtOperation> operations, [DisallowNull] string path, [DisallowNull] PropertyInfo property, object? originalValue, object? modifiedValue, [DisallowNull] CrdtMetadata originalMeta, [DisallowNull] CrdtMetadata modifiedMeta)
    {
        var originalNumeric = originalValue is not null ? Convert.ToDecimal(originalValue) : 0;
        var modifiedNumeric = modifiedValue is not null ? Convert.ToDecimal(modifiedValue) : 0;

        var delta = modifiedNumeric - originalNumeric;

        if (delta > 0)
        {
            var operation = new CrdtOperation(Guid.NewGuid(), replicaId, path, OperationType.Increment, delta, timestampProvider.Now());
            operations.Add(operation);
        }
    }

    /// <inheritdoc/>
    public void ApplyOperation([DisallowNull] object root, [DisallowNull] CrdtMetadata metadata, CrdtOperation operation)
    {
        if (operation.Type != OperationType.Increment)
        {
            throw new InvalidOperationException($"G-Counter strategy can only apply 'Increment' operations. Received '{operation.Type}'.");
        }

        if (metadata.SeenExceptions.Contains(operation))
        {
            return;
        }

        try
        {
            var increment = operation.Value is not null ? Convert.ToDecimal(operation.Value) : 0;
            if (increment <= 0)
            {
                // G-Counters only grow. Silently ignore non-positive increments.
                return;
            }

            var (parent, property, _) = PocoPathHelper.ResolvePath(root, operation.JsonPath);
            if (parent is null || property is null)
            {
                return;
            }

            var currentValue = property.GetValue(parent);
            var currentNumeric = currentValue is not null ? Convert.ToDecimal(currentValue) : 0;

            var newValue = currentNumeric + increment;
            var convertedValue = Convert.ChangeType(newValue, property.PropertyType);

            property.SetValue(parent, convertedValue);
        }
        finally
        {
            metadata.SeenExceptions.Add(operation);
        }
    }
}