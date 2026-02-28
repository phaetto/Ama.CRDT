namespace Ama.CRDT.Extensions;

using System.Collections.Generic;
using System.Numerics;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;

/// <summary>
/// Provides strongly-typed extension methods for <see cref="IIntentBuilder{TProperty}"/> to fluent build CRDT operations.
/// </summary>
public static class IntentBuilderExtensions
{
    /// <summary>
    /// Explicitly sets a value for a property or register.
    /// </summary>
    public static CrdtOperation Set<TProperty>(this IIntentBuilder<TProperty> builder, TProperty value)
    {
        return builder.Build(new SetIntent(value));
    }

    /// <summary>
    /// Explicitly increments or decrements a numeric value or counter.
    /// </summary>
    public static CrdtOperation Increment<TNumber>(this IIntentBuilder<TNumber> builder, TNumber value)
        where TNumber : INumber<TNumber>
    {
        return builder.Build(new IncrementIntent(value));
    }

    /// <summary>
    /// Explicitly adds a value to an unordered collection or set.
    /// </summary>
    public static CrdtOperation Add<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, TElement value)
    {
        return builder.Build(new AddIntent(value));
    }

    /// <summary>
    /// Explicitly removes a value from a collection or set.
    /// </summary>
    public static CrdtOperation Remove<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, TElement value)
    {
        return builder.Build(new RemoveValueIntent(value));
    }

    /// <summary>
    /// Explicitly inserts a value into a sequence at a specific index.
    /// </summary>
    public static CrdtOperation Insert<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, int index, TElement value)
    {
        return builder.Build(new InsertIntent(index, value));
    }

    /// <summary>
    /// Explicitly removes an item from a sequence by its index.
    /// </summary>
    public static CrdtOperation RemoveAt<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, int index)
    {
        return builder.Build(new RemoveIntent(index));
    }

    /// <summary>
    /// Explicitly sets a value at a specific index within a sequence.
    /// </summary>
    public static CrdtOperation SetIndex<TElement>(this IIntentBuilder<IEnumerable<TElement>> builder, int index, TElement value)
    {
        return builder.Build(new SetIndexIntent(index, value));
    }

    /// <summary>
    /// Explicitly sets a value for a specific key within a dictionary.
    /// </summary>
    public static CrdtOperation Set<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey key, TValue value)
        where TKey : notnull
    {
        return builder.Build(new MapSetIntent(key, value));
    }

    /// <summary>
    /// Explicitly removes a key and its associated value from a dictionary.
    /// </summary>
    public static CrdtOperation Remove<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey key)
        where TKey : notnull
    {
        return builder.Build(new MapRemoveIntent(key));
    }

    /// <summary>
    /// Explicitly increments or decrements a numeric value for a specific key within a dictionary.
    /// </summary>
    public static CrdtOperation Increment<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey key, TValue value)
        where TKey : notnull
        where TValue : INumber<TValue>
    {
        return builder.Build(new MapIncrementIntent(key, value));
    }

    /// <summary>
    /// Explicitly adds a node to a replicated tree.
    /// </summary>
    public static CrdtOperation AddNode(this IIntentBuilder<CrdtTree> builder, TreeNode node)
    {
        return builder.Build(new AddNodeIntent(node));
    }

    /// <summary>
    /// Explicitly removes a node from a replicated tree by its identifier.
    /// </summary>
    public static CrdtOperation RemoveNode(this IIntentBuilder<CrdtTree> builder, object nodeId)
    {
        return builder.Build(new RemoveNodeIntent(nodeId));
    }

    /// <summary>
    /// Explicitly moves a node in a replicated tree to a new parent.
    /// </summary>
    public static CrdtOperation MoveNode(this IIntentBuilder<CrdtTree> builder, object nodeId, object? newParentId)
    {
        return builder.Build(new MoveNodeIntent(nodeId, newParentId));
    }

    /// <summary>
    /// Explicitly adds a vertex to a graph.
    /// </summary>
    public static CrdtOperation AddVertex(this IIntentBuilder<CrdtGraph> builder, object vertex)
    {
        return builder.Build(new AddVertexIntent(vertex));
    }

    /// <summary>
    /// Explicitly removes a vertex from a graph.
    /// </summary>
    public static CrdtOperation RemoveVertex(this IIntentBuilder<CrdtGraph> builder, object vertex)
    {
        return builder.Build(new RemoveVertexIntent(vertex));
    }

    /// <summary>
    /// Explicitly adds an edge to a graph.
    /// </summary>
    public static CrdtOperation AddEdge(this IIntentBuilder<CrdtGraph> builder, Edge edge)
    {
        return builder.Build(new AddEdgeIntent(edge));
    }

    /// <summary>
    /// Explicitly removes an edge from a graph.
    /// </summary>
    public static CrdtOperation RemoveEdge(this IIntentBuilder<CrdtGraph> builder, Edge edge)
    {
        return builder.Build(new RemoveEdgeIntent(edge));
    }

    /// <summary>
    /// Explicitly casts a vote for a specific option.
    /// </summary>
    public static CrdtOperation Vote<TKey, TValue>(this IIntentBuilder<IEnumerable<KeyValuePair<TKey, TValue>>> builder, TKey voter, TValue option)
        where TKey : notnull
    {
        return builder.Build(new VoteIntent(voter, option!));
    }
}