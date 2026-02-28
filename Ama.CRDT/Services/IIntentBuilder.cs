namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;

/// <summary>
/// A builder interface for generating explicit CRDT operations in a strongly-typed manner.
/// It captures the document and property context, allowing extensions to provide type-safe intent methods.
/// </summary>
/// <typeparam name="TProperty">The type of the property being targeted. Marked as covariant (out) to support interface matching (e.g. List to IList).</typeparam>
public interface IIntentBuilder<out TProperty>
{
    /// <summary>
    /// Builds the final CRDT operation based on the provided intent.
    /// </summary>
    /// <param name="intent">The explicitly defined intent to apply.</param>
    /// <returns>The generated <see cref="CrdtOperation"/> ready to be applied or distributed.</returns>
    CrdtOperation Build(IOperationIntent intent);
}