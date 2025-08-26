namespace Ama.CRDT.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Ama.CRDT.Models;

/// <summary>
/// Defines the contract for a service that compares two versions of a data model and generates a CRDT patch.
/// The patcher is responsible for detecting changes and creating a list of operations that can be applied to other replicas.
/// </summary>
public interface ICrdtPatcher
{
    /// <summary>
    /// Generates a CRDT patch by comparing two versions of a document ("from" and "to").
    /// It recursively traverses the object trees, delegating to the appropriate CRDT strategy for each property to determine the correct operations.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="from">The original document state, including its data and metadata.</param>
    /// <param name="to">The modified document state, including its data and metadata.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the operations required to transform the "from" state to the "to" state.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="from"/>.Metadata or <paramref name="to"/>.Metadata is null.</exception>
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
    /// var metaV2 = metadataManager.Clone(metaV1); // Metadata is typically cloned and updated
    /// var crdtDocV2 = new CrdtDocument<MyDataObject>(docV2, metaV2);
    ///
    /// // Generate the patch
    /// // The patcher instance is typically created via ICrdtPatcherFactory for a specific replica.
    /// var patch = patcher.GeneratePatch(crdtDocV1, crdtDocV2);
    ///
    /// // The 'patch' will contain an Upsert operation for "$.value" with the value "World".
    /// ]]>
    /// </code>
    /// </example>
    CrdtPatch GeneratePatch<T>(CrdtDocument<T> from, CrdtDocument<T> to) where T : class;

    /// <summary>
    /// Recursively differentiates two objects, populating a list of CRDT operations.
    /// This method is the core of the patch generation logic and is designed for extensibility, allowing custom strategies to invoke it for nested objects.
    /// </summary>
    /// <param name="path">The current JSON path being compared (e.g., "$.user.name").</param>
    /// <param name="type">The type of the objects being compared.</param>
    /// <param name="fromObj">The original object instance (or null if it was added).</param>
    /// <param name="fromMeta">The metadata corresponding to the original document state.</param>
    /// <param name="toObj">The modified object instance (or null if it was removed).</param>
    /// <param name="toMeta">The metadata corresponding to the modified document state.</param>
    /// <param name="operations">The list to populate with generated <see cref="CrdtOperation"/> instances.</param>
    /// <param name="fromRoot">The root object of the original document, used for resolving relative paths if necessary.</param>
    /// <param name="toRoot">The root object of the modified document.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if required parameters like <paramref name="type"/>, <paramref name="fromMeta"/>, <paramref name="toMeta"/>, or <paramref name="operations"/> are null.</exception>
    /// <exception cref="System.ArgumentException">Thrown if <paramref name="path"/> is null or whitespace.</exception>
    /// <remarks>
    /// This method is typically not called directly by application code. It is used internally by the patcher and by custom <see cref="ICrdtStrategy"/> implementations
    /// that need to handle complex or nested data structures.
    /// </remarks>
    void DifferentiateObject(string path, [DisallowNull] Type type, object? fromObj, [DisallowNull] CrdtMetadata fromMeta, object? toObj, [DisallowNull] CrdtMetadata toMeta, [DisallowNull] List<CrdtOperation> operations, object? fromRoot, object? toRoot);
}