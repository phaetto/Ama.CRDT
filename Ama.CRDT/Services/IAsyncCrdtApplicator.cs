namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines the asynchronous contract for a service that applies a CRDT patch to a document.
/// The applicator is the central authority for conflict resolution and idempotency,
/// using an external metadata object to track state. Implementations should be thread-safe.
/// </summary>
public interface IAsyncCrdtApplicator
{
    /// <summary>
    /// Asynchronously applies a set of CRDT operations from a patch to a POCO document, modifying it in-place.
    /// The process is strategy-driven, idempotent, and resolves conflicts based on the provided metadata and operation timestamps.
    /// It ensures that operations are not applied more than once by consulting the version vector in the metadata.
    /// </summary>
    /// <typeparam name="T">The type of the POCO model representing the document structure.</typeparam>
    /// <param name="document">The <see cref="CrdtDocument{T}"/> containing the data and metadata to which the patch will be applied.</param>
    /// <param name="patch">A <see cref="CrdtPatch"/> containing the list of operations to apply. If the patch is null or contains no operations, the method returns without making changes.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="ApplyPatchResult{T}"/> with the document and a list of operations that could not be applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="document"/>, <paramref name="document"/>.Data, or <paramref name="document"/>.Metadata is null.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // Assume 'applicator', 'metadataManager', and 'timestampProvider' are injected.
    /// var myDoc = new MyDataObject { Counter = 5 };
    /// var metadata = metadataManager.Initialize(myDoc);
    /// var crdtDoc = new CrdtDocument<MyDataObject>(myDoc, metadata);
    /// 
    /// // A patch from another replica that increments the counter.
    /// var patch = new CrdtPatch(new List<CrdtOperation>
    /// {
    ///     new CrdtOperation(OperationType.Increment, "$.counter", 2, timestampProvider.Now(), "replica-B", 1)
    /// });
    /// 
    /// // Apply the patch to the local document asynchronously.
    /// var result = await applicator.ApplyPatchAsync(crdtDoc, patch, cancellationToken);
    /// 
    /// // After application, myDoc.Counter will be 7 and result.UnappliedOperations will be empty.
    /// ]]>
    /// </code>
    /// </example>
    Task<ApplyPatchResult<T>> ApplyPatchAsync<T>([DisallowNull] CrdtDocument<T> document, CrdtPatch patch, CancellationToken cancellationToken = default) where T : class;
}