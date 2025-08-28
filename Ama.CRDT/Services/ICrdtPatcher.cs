namespace Ama.CRDT.Services;
using Ama.CRDT.Models;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Defines the contract for a service that compares two versions of a data model and generates a CRDT patch.
/// The patcher is responsible for detecting changes and creating a list of operations that can be applied to other replicas.
/// </summary>
public interface ICrdtPatcher
{
    /// <summary>
    /// Generates a CRDT patch by comparing an original document state to a modified state.
    /// It recursively traverses the object trees, delegating to the appropriate CRDT strategy for each property to determine the correct operations.
    /// The timestamp for the change is generated internally using the configured <see cref="Providers.ICrdtTimestampProvider"/>.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="from">The original document state, including its data and metadata.</param>
    /// <param name="changed">The modified document data.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the operations required to transform the "from" state to the "changed" state.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="from"/>.Metadata or <paramref name="changed"/> is null.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // Assume 'patcher', 'metadataManager' are injected.
    /// var docV1 = new MyDataObject { Value = "Hello" };
    /// var metaV1 = metadataManager.Initialize(docV1);
    /// var crdtDocV1 = new CrdtDocument<MyDataObject>(docV1, metaV1);
    /// 
    /// // Simulate a change
    /// var docV2 = new MyDataObject { Value = "World" };
    ///
    /// // Generate the patch
    /// // The patcher instance is typically created via ICrdtPatcherFactory for a specific replica.
    /// var patch = patcher.GeneratePatch(crdtDocV1, docV2);
    ///
    /// // The 'patch' will contain an Upsert operation for "$.value" with the value "World".
    /// ]]>
    /// </code>
    /// </example>
    CrdtPatch GeneratePatch<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed) where T : class;

    /// <summary>
    /// Generates a CRDT patch with a specific timestamp by comparing an original document state to a modified state.
    /// This overload is useful for scenarios where the timestamp of the change is determined externally, such as in testing or when integrating with systems that have their own clock.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="from">The original document state, including its data and metadata.</param>
    /// <param name="changed">The modified document data.</param>
    /// <param name="changeTimestamp">The specific timestamp to assign to all operations in the generated patch.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the operations required to transform the "from" state to the "changed" state.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="from"/>.Metadata, <paramref name="changed"/>, or <paramref name="changeTimestamp"/> is null.</exception>
    CrdtPatch GeneratePatch<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed, [DisallowNull] ICrdtTimestamp changeTimestamp) where T : class;

    /// <summary>
    /// Recursively differentiates two objects, populating a list of CRDT operations.
    /// This method is the core of the patch generation logic and is designed for extensibility, allowing custom strategies to invoke it for nested objects.
    /// </summary>
    /// <param name="context">The context for the differentiation operation, containing all necessary parameters.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if the <paramref name="context"/> or any of its required properties are null.</exception>
    /// <exception cref="System.ArgumentException">Thrown if the path within the context is null or whitespace.</exception>
    /// <remarks>
    /// This method is typically not called directly by application code. It is used internally by the patcher implementation
    /// and by strategies that need to handle complex or nested data structures.
    /// </remarks>
    void DifferentiateObject([DisallowNull] DifferentiateObjectContext context);
}