namespace Ama.CRDT.Services;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Ama.CRDT.Models;

/// <summary>
/// Defines a factory for creating <see cref="IPatchContext"/> instances
/// for manually building <see cref="CrdtPatch"/> objects in a fluent manner.
/// </summary>
public interface ICrdtPatchBuilder
{
    /// <summary>
    /// Creates a new, stateful patch building session.
    /// </summary>
    /// <returns>A new instance of <see cref="IPatchContext"/> for building a patch.</returns>
    IPatchContext New();
}

/// <summary>
/// Represents a stateful session for building a single <see cref="CrdtPatch"/>.
/// This object is not thread-safe. Once the patch is built, the context cannot be reused.
/// </summary>
public interface IPatchContext
{
    /// <summary>
    /// Adds an 'Upsert' operation to the patch.
    /// </summary>
    /// <typeparam name="T">The root type of the document.</typeparam>
    /// <typeparam name="TProperty">The type of the property being set.</typeparam>
    /// <param name="pathExpression">A LINQ expression representing the JSON path to the property.</param>
    /// <param name="value">The new value for the property.</param>
    /// <param name="timestamp">Optional. The timestamp for the operation. If null, the current time is used.</param>
    /// <returns>The same <see cref="IPatchContext"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="pathExpression"/> is null.</exception>
    IPatchContext Upsert<T, TProperty>([DisallowNull] Expression<Func<T, TProperty>> pathExpression, TProperty value, ICrdtTimestamp? timestamp = null);

    /// <summary>
    /// Adds a 'Remove' operation to the patch.
    /// </summary>
    /// <typeparam name="T">The root type of the document.</typeparam>
    /// <param name="pathExpression">A LINQ expression representing the JSON path to the property.</param>
    /// <param name="timestamp">Optional. The timestamp for the operation. If null, the current time is used.</param>
    /// <returns>The same <see cref="IPatchContext"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="pathExpression"/> is null.</exception>
    IPatchContext Remove<T>([DisallowNull] Expression<Func<T, object?>> pathExpression, ICrdtTimestamp? timestamp = null);

    /// <summary>
    /// Adds an 'Increment' operation for a CRDT counter.
    /// </summary>
    /// <typeparam name="T">The root type of the document.</typeparam>
    /// <param name="pathExpression">A LINQ expression representing the JSON path to the counter property.</param>
    /// <param name="incrementBy">The value to increment the counter by. Can be negative for decrements.</param>
    /// <param name="timestamp">Optional. The timestamp for the operation. If null, the current time is used.</param>
    /// <returns>The same <see cref="IPatchContext"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="pathExpression"/> is null.</exception>
    IPatchContext Increment<T>([DisallowNull] Expression<Func<T, object?>> pathExpression, long incrementBy = 1, ICrdtTimestamp? timestamp = null);

    /// <summary>
    /// Builds the final <see cref="CrdtPatch"/> containing all the configured operations.
    /// Once called, this context should not be reused.
    /// </summary>
    /// <returns>A new <see cref="CrdtPatch"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <c>Build</c> is called more than once.</exception>
    CrdtPatch Build();
}