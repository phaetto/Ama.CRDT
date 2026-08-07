namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines the asynchronous contract for a service that merges two complete CRDT document states.
/// This represents the State-based (CvRDT) synchronization paradigm.
/// </summary>
public interface IAsyncCrdtMerger
{
    /// <summary>
    /// Asynchronously merges the state and metadata from a secondary document into a primary document.
    /// The primary document is mutated in place.
    /// </summary>
    /// <typeparam name="T">The type of the POCO model representing the document structure.</typeparam>
    /// <param name="primary">The primary document state, which will be updated with the merged results.</param>
    /// <param name="secondary">The secondary document state to merge into the primary.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if primary or secondary arguments are null.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // Assume 'asyncMerger' is injected.
    /// var crdtDocPrimary = new CrdtDocument<MyDataObject>(localDoc, localMeta);
    /// var crdtDocSecondary = new CrdtDocument<MyDataObject>(remoteDoc, remoteMeta);
    /// 
    /// // Asynchronously merge the remote state (secondary) into the local state (primary)
    /// await asyncMerger.MergeStateAsync(crdtDocPrimary, crdtDocSecondary, cancellationToken);
    /// 
    /// // crdtDocPrimary now contains the mathematically converged state of both documents
    /// ]]>
    /// </code>
    /// </example>
    Task MergeStateAsync<T>([DisallowNull] CrdtDocument<T> primary, [DisallowNull] CrdtDocument<T> secondary, CancellationToken cancellationToken = default) where T : class;
}