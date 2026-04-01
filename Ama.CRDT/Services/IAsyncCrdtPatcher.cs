namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines the asynchronous contract for a service that compares two versions of a data model and generates a CRDT patch.
/// The patcher is responsible for detecting changes and creating a list of operations that can be applied to other replicas.
/// </summary>
public interface IAsyncCrdtPatcher
{
    /// <summary>
    /// Asynchronously generates a CRDT patch by comparing an original document state to a modified state.
    /// It recursively traverses the object trees, delegating to the appropriate CRDT strategy for each property to determine the correct operations.
    /// The timestamp for the change is generated internally using the configured <see cref="Providers.ICrdtTimestampProvider"/>.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="from">The original document state, including its data and metadata.</param>
    /// <param name="changed">The modified document data.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="CrdtPatch"/> with the operations required to transform the "from" state to the "changed" state.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="from"/>.Metadata or <paramref name="changed"/> is null.</exception>
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
    /// // Generate the patch asynchronously
    /// // The patcher instance is typically created via ICrdtPatcherFactory for a specific replica.
    /// var patch = await patcher.GeneratePatchAsync(crdtDocV1, docV2, cancellationToken);
    ///
    /// // The 'patch' will contain an Upsert operation for "$.value" with the value "World".
    /// ]]>
    /// </code>
    /// </example>
    Task<CrdtPatch> GeneratePatchAsync<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Asynchronously generates a CRDT patch with a specific timestamp by comparing an original document state to a modified state.
    /// This overload is useful for scenarios where the timestamp of the change is determined externally, such as in testing or when integrating with systems that have their own clock.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="from">The original document state, including its data and metadata.</param>
    /// <param name="changed">The modified document data.</param>
    /// <param name="changeTimestamp">The specific timestamp to assign to all operations in the generated patch.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="CrdtPatch"/> with the operations required to transform the "from" state to the "changed" state.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="from"/>.Metadata, <paramref name="changed"/>, or <paramref name="changeTimestamp"/> is null.</exception>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var crdtDoc = new CrdtDocument<MyDataObject>(docV1, metaV1);
    /// var docV2 = new MyDataObject { Value = "World" };
    /// var customTs = new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    /// 
    /// var patch = await patcher.GeneratePatchAsync(crdtDoc, docV2, customTs, cancellationToken);
    /// ]]>
    /// </code>
    /// </example>
    Task<CrdtPatch> GeneratePatchAsync<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed, [DisallowNull] ICrdtTimestamp changeTimestamp, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Asynchronously creates a strongly-typed intent builder for a specific property, allowing you to generate operations
    /// using fluent extension methods like <c>.AddAsync()</c>, <c>.RemoveAsync()</c>, or <c>.IncrementAsync()</c> 
    /// without manually boxing values into intent structs.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <typeparam name="TProp">The type of the property being targeted.</typeparam>
    /// <param name="document">The original document state, including its data and metadata.</param>
    /// <param name="propertyExpression">An expression pinpointing the target property for the explicit intent.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="IIntentBuilder{TProperty}"/> that can be used to fluently build the operation.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var doc = new CrdtDocument<MyDataObject>(new MyDataObject(), metadata);
    /// 
    /// // Uses async extension methods from Ama.CRDT.Extensions (e.g., .SetAsync())
    /// // You can chain the task directly:
    /// var operation = await patcher.BuildOperationAsync(doc, x => x.Name, cancellationToken)
    ///                              .SetAsync("Alice", cancellationToken);
    /// ]]>
    /// </code>
    /// </example>
    Task<IIntentBuilder<TProp>> BuildOperationAsync<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Asynchronously creates a strongly-typed intent builder for a specific property with a specified timestamp, allowing you to generate operations
    /// using fluent extension methods like <c>.AddAsync()</c>, <c>.RemoveAsync()</c>, or <c>.IncrementAsync()</c> 
    /// without manually boxing values into intent structs.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <typeparam name="TProp">The type of the property being targeted.</typeparam>
    /// <param name="document">The original document state, including its data and metadata.</param>
    /// <param name="propertyExpression">An expression pinpointing the target property for the explicit intent.</param>
    /// <param name="timestamp">The specific timestamp to assign to the generated operation.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="IIntentBuilder{TProperty}"/> that can be used to fluently build the operation.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var doc = new CrdtDocument<MyDataObject>(new MyDataObject(), metadata);
    /// var customTs = new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    /// 
    /// // Uses async extension methods from Ama.CRDT.Extensions (e.g., .SetAsync())
    /// // You can chain the task directly:
    /// var operation = await patcher.BuildOperationAsync(doc, x => x.Name, customTs, cancellationToken)
    ///                              .SetAsync("Alice", cancellationToken);
    /// ]]>
    /// </code>
    /// </example>
    Task<IIntentBuilder<TProp>> BuildOperationAsync<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, [DisallowNull] ICrdtTimestamp timestamp, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Asynchronously generates a single CRDT operation explicitly based on a provided operation intent, bypassing the state diffing process.
    /// Uses an expression tree to strongly type and identify the targeted property.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <typeparam name="TProp">The type of the property being targeted.</typeparam>
    /// <param name="document">The original document state, including its data and metadata.</param>
    /// <param name="propertyExpression">An expression pinpointing the target property for the explicit intent.</param>
    /// <param name="intent">An object representing the intent to perform (e.g., <see cref="InsertIntent"/>).</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="CrdtOperation"/> representing the explicit operation.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var doc = new CrdtDocument<MyDataObject>(new MyDataObject(), metadata);
    /// var intent = new SetIntent("Alice");
    /// 
    /// var operation = await patcher.GenerateOperationAsync(doc, x => x.Name, intent, cancellationToken);
    /// ]]>
    /// </code>
    /// </example>
    Task<CrdtOperation> GenerateOperationAsync<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Asynchronously generates a single CRDT operation explicitly based on a provided operation intent and a specific timestamp, bypassing the state diffing process.
    /// Uses an expression tree to strongly type and identify the targeted property.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <typeparam name="TProp">The type of the property being targeted.</typeparam>
    /// <param name="document">The original document state, including its data and metadata.</param>
    /// <param name="propertyExpression">An expression pinpointing the target property for the explicit intent.</param>
    /// <param name="intent">An object representing the intent to perform (e.g., <see cref="InsertIntent"/>).</param>
    /// <param name="timestamp">The specific timestamp to assign to the generated operation.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="CrdtOperation"/> representing the explicit operation.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var doc = new CrdtDocument<MyDataObject>(new MyDataObject(), metadata);
    /// var intent = new SetIntent("Alice");
    /// var customTs = new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    /// 
    /// var operation = await patcher.GenerateOperationAsync(doc, x => x.Name, intent, customTs, cancellationToken);
    /// ]]>
    /// </code>
    /// </example>
    Task<CrdtOperation> GenerateOperationAsync<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, [DisallowNull] ICrdtTimestamp timestamp, CancellationToken cancellationToken = default) where T : class;
}