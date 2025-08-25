namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines the public facade service for orchestrating CRDT operations.
/// This service provides a high-level API that encapsulates patch generation and application
/// for a single, default replica configured during dependency injection.
/// </summary>
public interface ICrdtService
{
    /// <summary>
    /// Creates a patch that represents the changes between an original and a modified document.
    /// This method uses the default replica ID configured in <see cref="CrdtOptions"/>.
    /// </summary>
    /// <typeparam name="T">The type of the POCO data.</typeparam>
    /// <param name="original">The original document state, wrapping a POCO and its metadata.</param>
    /// <param name="modified">The modified document state, wrapping a POCO and its metadata. The metadata within this object will be updated with new timestamps for any changed properties.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the operations to transform the original into the modified document.</returns>
    /// <exception cref="ArgumentNullException">Thrown by the underlying patcher if metadata is missing.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var crdtService = serviceProvider.GetRequiredService<ICrdtService>();
    /// var metadataManager = serviceProvider.GetRequiredService<ICrdtMetadataManager>();
    ///
    /// var originalState = new MyPoco { Score = 10 };
    /// var originalDocument = new CrdtDocument<MyPoco>(originalState, metadataManager.Initialize(originalState));
    ///
    /// var modifiedState = new MyPoco { Score = 11 };
    /// // It's important to use the original metadata here so the patcher can see the previous state.
    /// var modifiedDocument = new CrdtDocument<MyPoco>(modifiedState, originalDocument.Metadata);
    ///
    /// // The patcher will update timestamps inside modifiedDocument.Metadata as it runs.
    /// CrdtPatch patch = crdtService.CreatePatch(originalDocument, modifiedDocument);
    /// ]]>
    /// </code>
    /// </example>
    CrdtPatch CreatePatch<T>(CrdtDocument<T> original, CrdtDocument<T> modified) where T : class;

    /// <summary>
    /// Applies a patch to a POCO document to produce a new, merged document state.
    /// The original document instance is modified in place.
    /// </summary>
    /// <typeparam name="T">The type of the POCO data.</typeparam>
    /// <param name="document">The POCO document to which the patch will be applied. This object will be mutated.</param>
    /// <param name="patch">The patch containing the changes.</param>
    /// <param name="metadata">The metadata object containing the current conflict resolution state for the document. This object will be mutated.</param>
    /// <returns>The original document instance with the patch applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown by the underlying applicator if document or metadata is null.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // Assume 'patch' was created and received from another replica.
    /// // Assume 'myPoco' is the object to merge into, and 'metadata' is its associated metadata.
    /// var crdtService = serviceProvider.GetRequiredService<ICrdtService>();
    /// 
    /// // The merge happens in-place on myPoco and its metadata object.
    /// crdtService.Merge(myPoco, patch, metadata);
    /// 
    /// Console.WriteLine($"New score: {myPoco.Score}");
    /// ]]>
    /// </code>
    /// </example>
    T Merge<T>([DisallowNull] T document, CrdtPatch patch, [DisallowNull] CrdtMetadata metadata) where T : class;
}