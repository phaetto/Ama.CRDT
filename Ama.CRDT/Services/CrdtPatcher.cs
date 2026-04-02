namespace Ama.CRDT.Services;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;

/// <inheritdoc/>
public sealed class CrdtPatcher(ICrdtStrategyProvider strategyProvider, ICrdtTimestampProvider timestampProvider, ReplicaContext replicaContext) : ICrdtPatcher
{
    private sealed class ClockState { public long Clock; }
    private readonly ConditionalWeakTable<CrdtMetadata, ClockState> issuedClocks = new();

    /// <inheritdoc/>
    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, T changed) where T : class
    {
        var changeTimestamp = timestampProvider.Now();
        return GeneratePatch(from, changed, changeTimestamp);
    }

    /// <inheritdoc/>
    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, T changed, ICrdtTimestamp changeTimestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(from.Metadata);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(changeTimestamp);

        var operations = new List<CrdtOperation>();
        var initialContext = new DifferentiateObjectContext(
            "$",
            typeof(T),
            from.Data,
            changed,
            from.Data,
            changed,
            from.Metadata,
            operations,
            changeTimestamp
        );
        
        // Process differentiations first. We pass a dummy clock (0) to the context 
        // because we will assign unique sequential clocks to each operation below.
        ProcessDifferentiations(initialContext, 0L);

        var replicaId = replicaContext.ReplicaId;
        var currentVectorClock = from.Metadata.VersionVector.TryGetValue(replicaId, out var currentClock) ? currentClock : 0L;
        
        var clockState = issuedClocks.GetOrCreateValue(from.Metadata);
        var localClock = Math.Max(currentVectorClock, clockState.Clock);

        // Calculate the global clock starting point for this replica across all tracked changes
        var globalClock = replicaContext.GlobalVersionVector.Versions.TryGetValue(replicaId, out var gc) ? gc : 0L;

        // Assign unique monotonically increasing local AND global clocks
        for (var i = 0; i < operations.Count; i++)
        {
            localClock++;
            globalClock++;
            operations[i] = operations[i] with { Clock = localClock, GlobalClock = globalClock, ReplicaId = replicaId };
            
            // Track the operations we've generated in our global causality vector
            replicaContext.GlobalVersionVector.Add(replicaId, globalClock);
        }

        clockState.Clock = localClock;

        // We DO NOT mutate from.Metadata.VersionVector here.
        // It is the responsibility of the caller to apply the generated patch locally 
        // to properly update both the VersionVector AND the strategy-specific metadata states.

        return new CrdtPatch(operations);
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent) where T : class
    {
        var changeTimestamp = timestampProvider.Now();
        return GenerateOperation(document, propertyExpression, intent, changeTimestamp);
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, ICrdtTimestamp timestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(document.Metadata);
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(propertyExpression);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(timestamp);

        var parseResult = PocoPathHelper.ParseExpression(propertyExpression);
        var strategy = strategyProvider.GetStrategy(parseResult.Property);

        var replicaId = replicaContext.ReplicaId;
        var currentVectorClock = document.Metadata.VersionVector.TryGetValue(replicaId, out var currentClock) ? currentClock : 0L;
        
        var clockState = issuedClocks.GetOrCreateValue(document.Metadata);
        var localClock = Math.Max(currentVectorClock, clockState.Clock);
        
        localClock++;
        clockState.Clock = localClock;

        var globalClock = replicaContext.GlobalVersionVector.Versions.TryGetValue(replicaId, out var gc) ? gc : 0L;
        globalClock++;

        var context = new GenerateOperationContext(
            DocumentRoot: document.Data,
            Metadata: document.Metadata,
            JsonPath: parseResult.JsonPath,
            Property: parseResult.Property,
            Intent: intent,
            Timestamp: timestamp,
            Clock: localClock
        );

        var operation = strategy.GenerateOperation(context);
        
        // Ensure the generated operation uses the correct replicaId and tracks both document and global clocks
        var finalOperation = operation with { ReplicaId = replicaId, Clock = localClock, GlobalClock = globalClock };
        replicaContext.GlobalVersionVector.Add(replicaId, globalClock);
        
        return finalOperation;
    }

    private void ProcessDifferentiations(DifferentiateObjectContext initialContext, long clock)
    {
        var queue = new Queue<DifferentiateObjectContext>();
        queue.Enqueue(initialContext);

        while (queue.Count > 0)
        {
            var context = queue.Dequeue();
            var (path, type, fromObj, toObj, fromRoot, toRoot, fromMeta, operations, changeTimestamp) = context;

            if (fromObj is null && toObj is null)
            {
                continue;
            }

            var properties = PocoPathHelper.GetCachedProperties(type);
            var isRoot = path == "$";

            foreach (var cached in properties)
            {
                var currentPath = isRoot ? cached.RootedPath : path + cached.PathSuffix;
                var fromValue = fromObj is not null ? cached.Accessor.Getter(fromObj) : null;
                var toValue = toObj is not null ? cached.Accessor.Getter(toObj) : null;

                var propertyType = cached.Property.PropertyType;
                var strategy = strategyProvider.GetStrategy(cached.Property);

                var isComplexLww = strategy is LwwStrategy 
                                   && propertyType.IsClass 
                                   && propertyType != typeof(string) 
                                   && !PocoPathHelper.IsCollection(propertyType);

                if (isComplexLww && (fromValue is null || toValue is not null))
                {
                    // Natively recurse into POCO properties
                    queue.Enqueue(new DifferentiateObjectContext(
                        currentPath, propertyType, fromValue, toValue, fromRoot, toRoot, fromMeta, operations, changeTimestamp));
                }
                else
                {
                    // Delegating to strategy, which could either be a terminal operation or an optimization (like emitting a parent Remove)
                    var nestedDiffs = new List<DifferentiateObjectContext>();
                    var strategyContext = new GeneratePatchContext(
                        operations, nestedDiffs, currentPath, cached.Property, fromValue, toValue, fromRoot, toRoot, fromMeta, changeTimestamp, clock);
                    
                    strategy.GeneratePatch(strategyContext);
                    
                    foreach (var nestedDiff in nestedDiffs)
                    {
                        queue.Enqueue(nestedDiff);
                    }
                }
            }
        }
    }
}