namespace Modern.CRDT.Services;

using Modern.CRDT.Models;

/// <summary>
/// Defines the contract for a service that compares two versions of a data model
/// and generates a CRDT patch based on Last-Writer-Wins (LWW) semantics and property-specific strategies.
/// </summary>
public interface IJsonCrdtPatcher
{
    /// <summary>
    /// Compares two instances of a POCO and generates a CRDT patch.
    /// It recursively traverses the object graph, using reflection to find properties
    /// and applying the appropriate CRDT strategy for each one.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="from">The original document, containing the original state of the data and its metadata.</param>
    /// <param name="to">The modified document, containing the new state of the data and its metadata.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the operations to transform 'from' into 'to'.</returns>
    CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class;
}