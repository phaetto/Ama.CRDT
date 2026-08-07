namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines the synchronous contract for a service that merges two complete CRDT document states.
/// This represents the State-based (CvRDT) synchronization paradigm, relying on calculating 
/// the Least Upper Bound (LUB) of the two states mathematically.
/// </summary>
public interface ICrdtMerger
{
    /// <summary>
    /// Merges the state and metadata from a secondary document into a primary document.
    /// The primary document is mutated in place.
    /// </summary>
    /// <typeparam name="T">The type of the POCO model representing the document structure.</typeparam>
    /// <param name="primary">The primary document state, which will be updated with the merged results.</param>
    /// <param name="secondary">The secondary document state to merge into the primary.</param>
    /// <exception cref="ArgumentNullException">Thrown if primary or secondary arguments are null.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // Assume 'merger' is injected.
    /// var crdtDocPrimary = new CrdtDocument<MyDataObject>(localDoc, localMeta);
    /// var crdtDocSecondary = new CrdtDocument<MyDataObject>(remoteDoc, remoteMeta);
    /// 
    /// // Merge the remote state (secondary) into the local state (primary)
    /// merger.MergeState(crdtDocPrimary, crdtDocSecondary);
    /// 
    /// // crdtDocPrimary now contains the mathematically converged state of both documents
    /// ]]>
    /// </code>
    /// </example>
    void MergeState<T>([DisallowNull] CrdtDocument<T> primary, [DisallowNull] CrdtDocument<T> secondary) where T : class;
}