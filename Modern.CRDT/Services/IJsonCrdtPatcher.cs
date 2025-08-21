using Modern.CRDT.Models;

namespace Modern.CRDT.Services;

/// <summary>
/// Defines the contract for a service that compares two JSON documents and generates a CRDT patch.
/// The patcher identifies differences and creates a list of operations that can be applied
/// to another replica to achieve eventual consistency.
/// </summary>
public interface IJsonCrdtPatcher
{
    /// <summary>
    /// Compares two <see cref="CrdtDocument"/> instances and generates a <see cref="CrdtPatch"/>
    /// containing the operations needed to transform the 'from' state into the 'to' state.
    /// The comparison uses Last-Writer-Wins (LWW) semantics: an operation is only generated
    /// for a change if the timestamp in the 'to' document's metadata is greater than the
    /// corresponding timestamp in the 'from' document's metadata.
    /// </summary>
    /// <param name="from">The original or source document state.</param>
    /// <param name="to">The modified or target document state.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the list of CRDT operations.</returns>
    CrdtPatch GeneratePatch(CrdtDocument from, CrdtDocument to);
}