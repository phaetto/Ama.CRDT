namespace Ama.CRDT.Extensions;

using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Intents.Decorators;

/// <summary>
/// Provides strongly-typed extension methods for <see cref="IIntentBuilder{TProperty}"/> to fluent build CRDT operations.
/// </summary>
public static class IntentBuilderExtensions
{
    // ====================================================================
    // Synchronous Extension Methods
    // ====================================================================

    /// <summary>
    /// Explicitly sets a value for a property or register.
    /// </summary>
    [CrdtIntentMapping(typeof(SetIntent))]
    public static CrdtOperation Set<TProperty>(this IIntentBuilder<TProperty> builder, TProperty value)
    {
        return builder.Build(new SetIntent(value));
    }

    /// <summary>
    /// Explicitly clears a property, collection, or state, resetting it.
    /// </summary>
    [CrdtIntentMapping(typeof(ClearIntent))]
    public static CrdtOperation Clear<TProperty>(this IIntentBuilder<TProperty> builder)
    {
        return builder.Build(new ClearIntent());
    }

    /// <summary>
    /// Explicitly increments or decrements a numeric value or counter.
    /// </summary>
    [CrdtIntentMapping(typeof(IncrementIntent))]
    public static CrdtOperation Increment<TNumber>(this IIntentBuilder<TNumber> builder, TNumber value)
        where TNumber : INumber<TNumber>
    {
        return builder.Build(new IncrementIntent(value));
    }

    /// <summary>
    /// Explicitly adds a value to an unordered collection or set.
    /// </summary>
    [CrdtIntentMapping(typeof(AddIntent))]
    public static CrdtOperation Add<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, TElement value)
    {
        return builder.Build(new AddIntent(value));
    }

    /// <summary>
    /// Explicitly removes a value from a collection or set.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveValueIntent))]
    public static CrdtOperation Remove<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, TElement value)
    {
        return builder.Build(new RemoveValueIntent(value));
    }

    /// <summary>
    /// Explicitly inserts a value into a sequence at a specific index.
    /// </summary>
    [CrdtIntentMapping(typeof(InsertIntent))]
    public static CrdtOperation Insert<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, int index, TElement value)
    {
        return builder.Build(new InsertIntent(index, value));
    }

    /// <summary>
    /// Explicitly removes an item from a sequence by its index.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveIntent))]
    public static CrdtOperation RemoveAt<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, int index)
    {
        return builder.Build(new RemoveIntent(index));
    }

    /// <summary>
    /// Explicitly sets a value at a specific index within a sequence.
    /// </summary>
    [CrdtIntentMapping(typeof(SetIndexIntent))]
    public static CrdtOperation SetIndex<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, int index, TElement value)
    {
        return builder.Build(new SetIndexIntent(index, value));
    }

    /// <summary>
    /// Explicitly sets a value for a specific key within a dictionary.
    /// </summary>
    [CrdtIntentMapping(typeof(MapSetIntent))]
    public static CrdtOperation Set<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey key, TValue value)
        where TKey : notnull
    {
        return builder.Build(new MapSetIntent(key, value));
    }

    /// <summary>
    /// Explicitly removes a key and its associated value from a dictionary.
    /// </summary>
    [CrdtIntentMapping(typeof(MapRemoveIntent))]
    public static CrdtOperation RemoveKey<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey key)
        where TKey : notnull
    {
        return builder.Build(new MapRemoveIntent(key));
    }

    /// <summary>
    /// Explicitly increments or decrements a numeric value for a specific key within a dictionary.
    /// </summary>
    [CrdtIntentMapping(typeof(MapIncrementIntent))]
    public static CrdtOperation Increment<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey key, TValue value)
        where TKey : notnull
        where TValue : INumber<TValue>
    {
        return builder.Build(new MapIncrementIntent(key, value));
    }

    /// <summary>
    /// Explicitly adds a node to a replicated tree.
    /// </summary>
    [CrdtIntentMapping(typeof(AddNodeIntent))]
    public static CrdtOperation AddNode(this IIntentBuilder<CrdtTree> builder, TreeNode node)
    {
        return builder.Build(new AddNodeIntent(node));
    }

    /// <summary>
    /// Explicitly removes a node from a replicated tree by its identifier.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveNodeIntent))]
    public static CrdtOperation RemoveNode(this IIntentBuilder<CrdtTree> builder, object nodeId)
    {
        return builder.Build(new RemoveNodeIntent(nodeId));
    }

    /// <summary>
    /// Explicitly moves a node in a replicated tree to a new parent.
    /// </summary>
    [CrdtIntentMapping(typeof(MoveNodeIntent))]
    public static CrdtOperation MoveNode(this IIntentBuilder<CrdtTree> builder, object nodeId, object? newParentId)
    {
        return builder.Build(new MoveNodeIntent(nodeId, newParentId));
    }

    /// <summary>
    /// Explicitly adds a vertex to a graph.
    /// </summary>
    [CrdtIntentMapping(typeof(AddVertexIntent))]
    public static CrdtOperation AddVertex(this IIntentBuilder<CrdtGraph> builder, object vertex)
    {
        return builder.Build(new AddVertexIntent(vertex));
    }

    /// <summary>
    /// Explicitly removes a vertex from a graph.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveVertexIntent))]
    public static CrdtOperation RemoveVertex(this IIntentBuilder<CrdtGraph> builder, object vertex)
    {
        return builder.Build(new RemoveVertexIntent(vertex));
    }

    /// <summary>
    /// Explicitly adds an edge to a graph.
    /// </summary>
    [CrdtIntentMapping(typeof(AddEdgeIntent))]
    public static CrdtOperation AddEdge(this IIntentBuilder<CrdtGraph> builder, Edge edge)
    {
        return builder.Build(new AddEdgeIntent(edge));
    }

    /// <summary>
    /// Explicitly removes an edge from a graph.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveEdgeIntent))]
    public static CrdtOperation RemoveEdge(this IIntentBuilder<CrdtGraph> builder, Edge edge)
    {
        return builder.Build(new RemoveEdgeIntent(edge));
    }

    /// <summary>
    /// Explicitly casts a vote for a specific option.
    /// </summary>
    [CrdtIntentMapping(typeof(VoteIntent))]
    public static CrdtOperation Vote<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey voter, TValue option)
        where TKey : notnull
    {
        return builder.Build(new VoteIntent(voter, option!));
    }

    /// <summary>
    /// Explicitly clears the state within an Epoch-bound decorator, bumping its epoch to invalidate older operations.
    /// </summary>
    [CrdtIntentMapping(typeof(EpochClearIntent))]
    public static CrdtOperation ClearEpoch<TProperty>(this IIntentBuilder<TProperty> builder)
    {
        return builder.Build(new EpochClearIntent());
    }

    // ====================================================================
    // Asynchronous Extension Methods (IIntentBuilder)
    // ====================================================================

    /// <summary>
    /// Explicitly sets a value for a property or register asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(SetIntent))]
    public static Task<CrdtOperation> SetAsync<TProperty>(this IIntentBuilder<TProperty> builder, TProperty value, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new SetIntent(value), cancellationToken);
    }

    /// <summary>
    /// Explicitly clears a property, collection, or state, resetting it asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(ClearIntent))]
    public static Task<CrdtOperation> ClearAsync<TProperty>(this IIntentBuilder<TProperty> builder, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new ClearIntent(), cancellationToken);
    }

    /// <summary>
    /// Explicitly increments or decrements a numeric value or counter asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(IncrementIntent))]
    public static Task<CrdtOperation> IncrementAsync<TNumber>(this IIntentBuilder<TNumber> builder, TNumber value, CancellationToken cancellationToken = default)
        where TNumber : INumber<TNumber>
    {
        return builder.BuildAsync(new IncrementIntent(value), cancellationToken);
    }

    /// <summary>
    /// Explicitly adds a value to an unordered collection or set asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(AddIntent))]
    public static Task<CrdtOperation> AddAsync<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, TElement value, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new AddIntent(value), cancellationToken);
    }

    /// <summary>
    /// Explicitly removes a value from a collection or set asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveValueIntent))]
    public static Task<CrdtOperation> RemoveAsync<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, TElement value, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new RemoveValueIntent(value), cancellationToken);
    }

    /// <summary>
    /// Explicitly inserts a value into a sequence at a specific index asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(InsertIntent))]
    public static Task<CrdtOperation> InsertAsync<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, int index, TElement value, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new InsertIntent(index, value), cancellationToken);
    }

    /// <summary>
    /// Explicitly removes an item from a sequence by its index asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveIntent))]
    public static Task<CrdtOperation> RemoveAtAsync<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, int index, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new RemoveIntent(index), cancellationToken);
    }

    /// <summary>
    /// Explicitly sets a value at a specific index within a sequence asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(SetIndexIntent))]
    public static Task<CrdtOperation> SetIndexAsync<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, int index, TElement value, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new SetIndexIntent(index, value), cancellationToken);
    }

    /// <summary>
    /// Explicitly sets a value for a specific key within a dictionary asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(MapSetIntent))]
    public static Task<CrdtOperation> SetAsync<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey key, TValue value, CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        return builder.BuildAsync(new MapSetIntent(key, value), cancellationToken);
    }

    /// <summary>
    /// Explicitly removes a key and its associated value from a dictionary asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(MapRemoveIntent))]
    public static Task<CrdtOperation> RemoveKeyAsync<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey key, CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        return builder.BuildAsync(new MapRemoveIntent(key), cancellationToken);
    }

    /// <summary>
    /// Explicitly increments or decrements a numeric value for a specific key within a dictionary asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(MapIncrementIntent))]
    public static Task<CrdtOperation> IncrementAsync<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey key, TValue value, CancellationToken cancellationToken = default)
        where TKey : notnull
        where TValue : INumber<TValue>
    {
        return builder.BuildAsync(new MapIncrementIntent(key, value), cancellationToken);
    }

    /// <summary>
    /// Explicitly adds a node to a replicated tree asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(AddNodeIntent))]
    public static Task<CrdtOperation> AddNodeAsync(this IIntentBuilder<CrdtTree> builder, TreeNode node, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new AddNodeIntent(node), cancellationToken);
    }

    /// <summary>
    /// Explicitly removes a node from a replicated tree by its identifier asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveNodeIntent))]
    public static Task<CrdtOperation> RemoveNodeAsync(this IIntentBuilder<CrdtTree> builder, object nodeId, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new RemoveNodeIntent(nodeId), cancellationToken);
    }

    /// <summary>
    /// Explicitly moves a node in a replicated tree to a new parent asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(MoveNodeIntent))]
    public static Task<CrdtOperation> MoveNodeAsync(this IIntentBuilder<CrdtTree> builder, object nodeId, object? newParentId, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new MoveNodeIntent(nodeId, newParentId), cancellationToken);
    }

    /// <summary>
    /// Explicitly adds a vertex to a graph asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(AddVertexIntent))]
    public static Task<CrdtOperation> AddVertexAsync(this IIntentBuilder<CrdtGraph> builder, object vertex, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new AddVertexIntent(vertex), cancellationToken);
    }

    /// <summary>
    /// Explicitly removes a vertex from a graph asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveVertexIntent))]
    public static Task<CrdtOperation> RemoveVertexAsync(this IIntentBuilder<CrdtGraph> builder, object vertex, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new RemoveVertexIntent(vertex), cancellationToken);
    }

    /// <summary>
    /// Explicitly adds an edge to a graph asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(AddEdgeIntent))]
    public static Task<CrdtOperation> AddEdgeAsync(this IIntentBuilder<CrdtGraph> builder, Edge edge, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new AddEdgeIntent(edge), cancellationToken);
    }

    /// <summary>
    /// Explicitly removes an edge from a graph asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveEdgeIntent))]
    public static Task<CrdtOperation> RemoveEdgeAsync(this IIntentBuilder<CrdtGraph> builder, Edge edge, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new RemoveEdgeIntent(edge), cancellationToken);
    }

    /// <summary>
    /// Explicitly casts a vote for a specific option asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(VoteIntent))]
    public static Task<CrdtOperation> VoteAsync<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey voter, TValue option, CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        return builder.BuildAsync(new VoteIntent(voter, option!), cancellationToken);
    }

    /// <summary>
    /// Explicitly clears the state within an Epoch-bound decorator, bumping its epoch to invalidate older operations asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(EpochClearIntent))]
    public static Task<CrdtOperation> ClearEpochAsync<TProperty>(this IIntentBuilder<TProperty> builder, CancellationToken cancellationToken = default)
    {
        return builder.BuildAsync(new EpochClearIntent(), cancellationToken);
    }

    // ====================================================================
    // Asynchronous Extension Methods (Task<IIntentBuilder>)
    // ====================================================================

    /// <summary>
    /// Explicitly sets a value for a property or register asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(SetIntent))]
    public static async Task<CrdtOperation> SetAsync<TProperty>(this Task<IIntentBuilder<TProperty>> builderTask, TProperty value, CancellationToken cancellationToken = default)
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new SetIntent(value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly clears a property, collection, or state, resetting it asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(ClearIntent))]
    public static async Task<CrdtOperation> ClearAsync<TProperty>(this Task<IIntentBuilder<TProperty>> builderTask, CancellationToken cancellationToken = default)
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new ClearIntent(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly increments or decrements a numeric value or counter asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(IncrementIntent))]
    public static async Task<CrdtOperation> IncrementAsync<TNumber>(this Task<IIntentBuilder<TNumber>> builderTask, TNumber value, CancellationToken cancellationToken = default)
        where TNumber : INumber<TNumber>
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new IncrementIntent(value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly adds a value to an unordered collection or set asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(AddIntent))]
    public static async Task<CrdtOperation> AddAsync<TCollection, TElement>(this Task<IIntentBuilder<TCollection>> builderTask, TElement value, CancellationToken cancellationToken = default)
        where TCollection : IEnumerable<TElement>
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new AddIntent(value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly removes a value from a collection or set asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveValueIntent))]
    public static async Task<CrdtOperation> RemoveAsync<TCollection, TElement>(this Task<IIntentBuilder<TCollection>> builderTask, TElement value, CancellationToken cancellationToken = default)
        where TCollection : IEnumerable<TElement>
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new RemoveValueIntent(value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly inserts a value into a sequence at a specific index asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(InsertIntent))]
    public static async Task<CrdtOperation> InsertAsync<TCollection, TElement>(this Task<IIntentBuilder<TCollection>> builderTask, int index, TElement value, CancellationToken cancellationToken = default)
        where TCollection : IEnumerable<TElement>
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new InsertIntent(index, value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly removes an item from a sequence by its index asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveIntent))]
    public static async Task<CrdtOperation> RemoveAtAsync<TCollection>(this Task<IIntentBuilder<TCollection>> builderTask, int index, CancellationToken cancellationToken = default)
        where TCollection : System.Collections.IEnumerable
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new RemoveIntent(index), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly sets a value at a specific index within a sequence asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(SetIndexIntent))]
    public static async Task<CrdtOperation> SetIndexAsync<TCollection, TElement>(this Task<IIntentBuilder<TCollection>> builderTask, int index, TElement value, CancellationToken cancellationToken = default)
        where TCollection : IEnumerable<TElement>
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new SetIndexIntent(index, value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly sets a value for a specific key within a dictionary asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(MapSetIntent))]
    public static async Task<CrdtOperation> SetAsync<TMap, TKey, TValue>(this Task<IIntentBuilder<TMap>> builderTask, TKey key, TValue value, CancellationToken cancellationToken = default)
        where TMap : IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : notnull
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new MapSetIntent(key, value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly removes a key and its associated value from a dictionary asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(MapRemoveIntent))]
    public static async Task<CrdtOperation> RemoveKeyAsync<TMap, TKey>(this Task<IIntentBuilder<TMap>> builderTask, TKey key, CancellationToken cancellationToken = default)
        where TMap : System.Collections.IEnumerable
        where TKey : notnull
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new MapRemoveIntent(key), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly increments or decrements a numeric value for a specific key within a dictionary asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(MapIncrementIntent))]
    public static async Task<CrdtOperation> IncrementAsync<TMap, TKey, TValue>(this Task<IIntentBuilder<TMap>> builderTask, TKey key, TValue value, CancellationToken cancellationToken = default)
        where TMap : IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : notnull
        where TValue : INumber<TValue>
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new MapIncrementIntent(key, value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly adds a node to a replicated tree asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(AddNodeIntent))]
    public static async Task<CrdtOperation> AddNodeAsync(this Task<IIntentBuilder<CrdtTree>> builderTask, TreeNode node, CancellationToken cancellationToken = default)
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new AddNodeIntent(node), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly removes a node from a replicated tree by its identifier asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveNodeIntent))]
    public static async Task<CrdtOperation> RemoveNodeAsync(this Task<IIntentBuilder<CrdtTree>> builderTask, object nodeId, CancellationToken cancellationToken = default)
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new RemoveNodeIntent(nodeId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly moves a node in a replicated tree to a new parent asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(MoveNodeIntent))]
    public static async Task<CrdtOperation> MoveNodeAsync(this Task<IIntentBuilder<CrdtTree>> builderTask, object nodeId, object? newParentId, CancellationToken cancellationToken = default)
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new MoveNodeIntent(nodeId, newParentId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly adds a vertex to a graph asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(AddVertexIntent))]
    public static async Task<CrdtOperation> AddVertexAsync(this Task<IIntentBuilder<CrdtGraph>> builderTask, object vertex, CancellationToken cancellationToken = default)
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new AddVertexIntent(vertex), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly removes a vertex from a graph asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveVertexIntent))]
    public static async Task<CrdtOperation> RemoveVertexAsync(this Task<IIntentBuilder<CrdtGraph>> builderTask, object vertex, CancellationToken cancellationToken = default)
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new RemoveVertexIntent(vertex), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly adds an edge to a graph asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(AddEdgeIntent))]
    public static async Task<CrdtOperation> AddEdgeAsync(this Task<IIntentBuilder<CrdtGraph>> builderTask, Edge edge, CancellationToken cancellationToken = default)
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new AddEdgeIntent(edge), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly removes an edge from a graph asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(RemoveEdgeIntent))]
    public static async Task<CrdtOperation> RemoveEdgeAsync(this Task<IIntentBuilder<CrdtGraph>> builderTask, Edge edge, CancellationToken cancellationToken = default)
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new RemoveEdgeIntent(edge), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly casts a vote for a specific option asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(VoteIntent))]
    public static async Task<CrdtOperation> VoteAsync<TMap, TKey, TValue>(this Task<IIntentBuilder<TMap>> builderTask, TKey voter, TValue option, CancellationToken cancellationToken = default)
        where TMap : IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : notnull
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new VoteIntent(voter, option!), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Explicitly clears the state within an Epoch-bound decorator, bumping its epoch to invalidate older operations asynchronously.
    /// </summary>
    [CrdtIntentMapping(typeof(EpochClearIntent))]
    public static async Task<CrdtOperation> ClearEpochAsync<TProperty>(this Task<IIntentBuilder<TProperty>> builderTask, CancellationToken cancellationToken = default)
    {
        var builder = await builderTask.ConfigureAwait(false);
        return await builder.BuildAsync(new EpochClearIntent(), cancellationToken).ConfigureAwait(false);
    }
}