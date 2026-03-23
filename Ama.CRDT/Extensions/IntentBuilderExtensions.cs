namespace Ama.CRDT.Extensions;

using System.Collections.Generic;
using System.Numerics;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Intents.Decorators;

/// <summary>
/// Provides strongly-typed extension methods for <see cref="IIntentBuilder{TProperty}"/> to fluent build CRDT operations.
/// </summary>
public static class IntentBuilderExtensions
{
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
    public static CrdtOperation Remove<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey key)
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
}