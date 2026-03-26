namespace Ama.CRDT.Services.GarbageCollection;

using System;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Models;

/// <summary>
/// A compaction policy that combines multiple policies. An item is safe to compact if ANY of the underlying policies consider it safe.
/// </summary>
public sealed class CompositeCompactionPolicy : ICompactionPolicy
{
    private readonly IReadOnlyList<ICompactionPolicy> _policies;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeCompactionPolicy"/> class.
    /// </summary>
    /// <param name="policies">The collection of policies to evaluate.</param>
    public CompositeCompactionPolicy(IEnumerable<ICompactionPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        _policies = policies.ToList();
    }

    /// <inheritdoc/>
    public bool IsSafeToCompact(ICrdtTimestamp timestamp)
    {
        for (int i = 0; i < _policies.Count; i++)
        {
            if (_policies[i].IsSafeToCompact(timestamp))
            {
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc/>
    public bool IsSafeToCompact(string replicaId, long version)
    {
        for (int i = 0; i < _policies.Count; i++)
        {
            if (_policies[i].IsSafeToCompact(replicaId, version))
            {
                return true;
            }
        }
        return false;
    }
}