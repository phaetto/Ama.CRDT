namespace Ama.CRDT.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Helpers;
using Microsoft.Extensions.Options;

/// <inheritdoc/>
public sealed class CrdtPatchBuilder(ICrdtTimestampProvider timestampProvider, IOptions<CrdtOptions> options) : ICrdtPatchBuilder
{
    private readonly CrdtOptions options = options.Value;

    /// <inheritdoc/>
    public IPatchContext New()
    {
        return new PatchContext(timestampProvider, options);
    }

    private sealed class PatchContext(ICrdtTimestampProvider timestampProvider, CrdtOptions options) : IPatchContext
    {
        private readonly List<CrdtOperation> operations = [];
        private bool isBuilt;

        /// <inheritdoc/>
        public IPatchContext Upsert<T, TProperty>(Expression<Func<T, TProperty>> pathExpression, TProperty value, ICrdtTimestamp? timestamp = null)
        {
            EnsureNotBuilt();
            ArgumentNullException.ThrowIfNull(pathExpression);

            var jsonPath = ExpressionToJsonPathConverter.Convert(pathExpression);
            var op = new CrdtOperation(
                Guid.NewGuid(),
                options.ReplicaId,
                jsonPath,
                OperationType.Upsert,
                value,
                timestamp ?? timestampProvider.Now()
            );
            operations.Add(op);
            return this;
        }

        /// <inheritdoc/>
        public IPatchContext Remove<T>([DisallowNull] Expression<Func<T, object?>> pathExpression, ICrdtTimestamp? timestamp = null)
        {
            EnsureNotBuilt();
            ArgumentNullException.ThrowIfNull(pathExpression);

            var jsonPath = ExpressionToJsonPathConverter.Convert(pathExpression);
            var op = new CrdtOperation(
                Guid.NewGuid(),
                options.ReplicaId,
                jsonPath,
                OperationType.Remove,
                null,
                timestamp ?? timestampProvider.Now()
            );
            operations.Add(op);
            return this;
        }

        /// <inheritdoc/>
        public IPatchContext Increment<T>([DisallowNull] Expression<Func<T, object?>> pathExpression, long incrementBy = 1, ICrdtTimestamp? timestamp = null)
        {
            EnsureNotBuilt();
            ArgumentNullException.ThrowIfNull(pathExpression);

            var jsonPath = ExpressionToJsonPathConverter.Convert(pathExpression);
            var op = new CrdtOperation(
                Guid.NewGuid(),
                options.ReplicaId,
                jsonPath,
                OperationType.Increment,
                incrementBy,
                timestamp ?? timestampProvider.Now()
            );
            operations.Add(op);
            return this;
        }

        /// <inheritdoc/>
        public CrdtPatch Build()
        {
            EnsureNotBuilt();
            isBuilt = true;
            return new CrdtPatch([.. operations]);
        }

        private void EnsureNotBuilt()
        {
            if (isBuilt)
            {
                throw new InvalidOperationException("Patch has already been built and the context cannot be reused.");
            }
        }
    }
}