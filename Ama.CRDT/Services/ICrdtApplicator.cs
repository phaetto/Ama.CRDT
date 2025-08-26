namespace Ama.CRDT.Services;

using Ama.CRDT.Models;

/// <summary>
/// Defines the contract for a service that applies a CRDT patch to a document.
/// The applicator is the central authority for conflict resolution and idempotency,
/// using an external metadata object to track state. Implementations should be thread-safe.
/// </summary>
public interface ICrdtApplicator
{
    /// <summary>
    /// Applies a set of CRDT operations from a patch to a POCO document, modifying it in-place.
    /// The process is strategy-driven, idempotent, and resolves conflicts based on the provided metadata and operation timestamps.
    /// It ensures that operations are not applied more than once by consulting the version vector in the metadata.
    /// </summary>
    /// <typeparam name="T">The type of the POCO model representing the document structure.</typeparam>
    /// <param name="document">The <see cref="CrdtDocument{T}"/> containing the data and metadata to which the patch will be applied.</param>
    /// <param name="patch">A <see cref="CrdtPatch"/> containing the list of operations to apply. If the patch is null or contains no operations, the method returns without making changes.</param>
    /// <returns>The original document data instance with the patch applied.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="document"/>, <paramref name="document"/>.Data, or <paramref name="document"/>.Metadata is null.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if an operation targets an invalid path or a strategy fails to apply a change.</exception>
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
    ///     new CrdtOperation(OperationType.Increment, "$.counter", 2, timestampProvider.Now(), "replica-B")
    /// });
    /// 
    /// // Apply the patch to the local document.
    /// applicator.ApplyPatch(crdtDoc, patch);
    /// 
    /// // After application, myDoc.Counter will be 7.
    /// ]]>
    /// </code>
    /// </example>
    T ApplyPatch<T>(CrdtDocument<T> document, CrdtPatch patch) where T : class;
}