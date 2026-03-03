namespace Ama.CRDT.Services;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;

/// <inheritdoc/>
public sealed class CrdtPatcher(ICrdtStrategyProvider strategyProvider, ICrdtTimestampProvider timestampProvider, ReplicaContext replicaContext) : ICrdtPatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly ConcurrentDictionary<Type, CachedPropertyMetadata[]> PropertyCache = new();

    /// <inheritdoc/>
    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, T changed) where T : class
    {
        var changeTimestamp = timestampProvider.Now();
        return GeneratePatch(from, changed, changeTimestamp);
    }

    /// <inheritdoc/>
    public CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, T changed, ICrdtTimestamp changeTimestamp) where T : class
    {
        ArgumentNullException.ThrowIfNull(from);
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
        
        ProcessDifferentiations(initialContext);

        var replicaId = replicaContext.ReplicaId;
        var localClock = from.Metadata.VersionVector.TryGetValue(replicaId, out var currentClock) ? currentClock : 0L;

        for (int i = 0; i < operations.Count; i++)
        {
            localClock++;
            var op = operations[i];
            operations[i] = op with { ReplicaId = replicaId, Clock = localClock };
        }

        // We DO NOT mutate from.Metadata.VersionVector here.
        // It is the responsibility of the caller to apply the generated patch locally 
        // to properly update both the VersionVector AND the strategy-specific metadata states.

        return new CrdtPatch(operations);
    }

    /// <inheritdoc/>
    public IIntentBuilder<TProp> BuildOperation<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(propertyExpression);

        return new IntentBuilder<T, TProp>(this, document, propertyExpression);
    }

    /// <inheritdoc/>
    public CrdtOperation GenerateOperation<T, TProp>(CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent) where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Metadata);
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(propertyExpression);
        ArgumentNullException.ThrowIfNull(intent);

        var parseResult = ParseExpression(propertyExpression);
        var strategy = strategyProvider.GetStrategy(parseResult.Property);

        var context = new GenerateOperationContext(
            DocumentRoot: document.Data,
            Metadata: document.Metadata,
            JsonPath: parseResult.JsonPath,
            Property: parseResult.Property,
            Intent: intent,
            Timestamp: timestampProvider.Now(),
            ReplicaId: replicaContext.ReplicaId
        );

        var operation = strategy.GenerateOperation(context);

        var replicaId = replicaContext.ReplicaId;
        var localClock = document.Metadata.VersionVector.TryGetValue(replicaId, out var currentClock) ? currentClock : 0L;
        localClock++;

        return operation with { ReplicaId = replicaId, Clock = localClock };
    }

    private void ProcessDifferentiations(DifferentiateObjectContext initialContext)
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

            var properties = GetCachedProperties(type);
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
                                   && !IsCollection(propertyType);

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
                        operations, nestedDiffs, currentPath, cached.Property, fromValue, toValue, fromRoot, toRoot, fromMeta, changeTimestamp);
                    
                    strategy.GeneratePatch(strategyContext);
                    
                    foreach (var nestedDiff in nestedDiffs)
                    {
                        queue.Enqueue(nestedDiff);
                    }
                }
            }
        }
    }

    private static CachedPropertyMetadata[] GetCachedProperties(Type type)
    {
        return PropertyCache.GetOrAdd(type, static t =>
            [.. t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .Select(p => 
                {
                    var jsonPropertyName = SerializerOptions.PropertyNamingPolicy?.ConvertName(p.Name) ?? p.Name;
                    return new CachedPropertyMetadata(p, jsonPropertyName);
                })]);
    }

    private static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    private static ParseResult ParseExpression<T, TProp>(Expression<Func<T, TProp>> expression)
    {
        var memberExpr = GetMemberExpression(expression.Body);
        
        if (memberExpr?.Member is not PropertyInfo propInfo)
        {
            throw new ArgumentException("Expression must be a simple property access.", nameof(expression));
        }

        var jsonPath = BuildJsonPath(memberExpr);
        return new ParseResult(jsonPath, propInfo);
    }

    private static MemberExpression? GetMemberExpression(Expression expression)
    {
        if (expression is UnaryExpression unary)
        {
            return unary.Operand as MemberExpression;
        }

        return expression as MemberExpression;
    }

    private static string BuildJsonPath(MemberExpression memberExpression)
    {
        var parts = new List<string>();
        var current = (Expression)memberExpression;

        while (current is MemberExpression me)
        {
            var propName = me.Member.Name;
            var jsonName = SerializerOptions.PropertyNamingPolicy?.ConvertName(propName) ?? propName;
            parts.Add(jsonName);
            current = me.Expression;
        }

        parts.Reverse();
        return "$" + (parts.Count > 0 ? "." + string.Join(".", parts) : string.Empty);
    }

    internal sealed class CachedPropertyMetadata(PropertyInfo property, string jsonPropertyName)
    {
        public PropertyInfo Property { get; } = property;
        public string PathSuffix { get; } = $".{jsonPropertyName}";
        public string RootedPath { get; } = $"$.{jsonPropertyName}";
        public PocoPathHelper.PropertyAccessor Accessor { get; } = PocoPathHelper.GetAccessor(property);
    }

    private readonly record struct ParseResult(string JsonPath, PropertyInfo Property);

    private sealed class IntentBuilder<TModel, TProp>(
        ICrdtPatcher patcher,
        CrdtDocument<TModel> document,
        Expression<Func<TModel, TProp>> propertyExpression) : IIntentBuilder<TProp>
        where TModel : class
    {
        public CrdtOperation Build(IOperationIntent intent)
        {
            return patcher.GenerateOperation(document, propertyExpression, intent);
        }
    }
}