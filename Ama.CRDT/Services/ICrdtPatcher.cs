namespace Ama.CRDT.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

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
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var crdtDoc = new CrdtDocument<MyDataObject>(docV1, metaV1);
    /// var docV2 = new MyDataObject { Value = "World" };
    /// var customTs = new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    /// 
    /// var patch = patcher.GeneratePatch(crdtDoc, docV2, customTs);
    /// ]]>
    /// </code>
    /// </example>
    CrdtPatch GeneratePatch<T>([DisallowNull] CrdtDocument<T> from, [DisallowNull] T changed, [DisallowNull] ICrdtTimestamp changeTimestamp) where T : class;

    /// <summary>
    /// Creates a strongly-typed intent builder for a specific property, allowing you to generate operations
    /// using fluent extension methods like <c>.Add()</c>, <c>.Remove()</c>, or <c>.Increment()</c> 
    /// without manually boxing values into intent structs.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <typeparam name="TProp">The type of the property being targeted.</typeparam>
    /// <param name="document">The original document state, including its data and metadata.</param>
    /// <param name="propertyExpression">An expression pinpointing the target property for the explicit intent.</param>
    /// <returns>An <see cref="IIntentBuilder{TProperty}"/> that can be used to fluently build the operation.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var doc = new CrdtDocument<MyDataObject>(new MyDataObject(), metadata);
    /// 
    /// // Uses extension methods from Ama.CRDT.Extensions (e.g., .Set())
    /// var operation = patcher.BuildOperation(doc, x => x.Name).Set("Alice");
    /// ]]>
    /// </code>
    /// </example>
    IIntentBuilder<TProp> BuildOperation<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression) where T : class;

    /// <summary>
    /// Creates a strongly-typed intent builder for a specific property with a specified timestamp, allowing you to generate operations
    /// using fluent extension methods like <c>.Add()</c>, <c>.Remove()</c>, or <c>.Increment()</c> 
    /// without manually boxing values into intent structs.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <typeparam name="TProp">The type of the property being targeted.</typeparam>
    /// <param name="document">The original document state, including its data and metadata.</param>
    /// <param name="propertyExpression">An expression pinpointing the target property for the explicit intent.</param>
    /// <param name="timestamp">The specific timestamp to assign to the generated operation.</param>
    /// <returns>An <see cref="IIntentBuilder{TProperty}"/> that can be used to fluently build the operation.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var doc = new CrdtDocument<MyDataObject>(new MyDataObject(), metadata);
    /// var customTs = new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    /// 
    /// var operation = patcher.BuildOperation(doc, x => x.Name, customTs).Set("Alice");
    /// ]]>
    /// </code>
    /// </example>
    IIntentBuilder<TProp> BuildOperation<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, [DisallowNull] ICrdtTimestamp timestamp) where T : class;

    /// <summary>
    /// Generates a single CRDT operation explicitly based on a provided operation intent, bypassing the state diffing process.
    /// Uses an expression tree to strongly type and identify the targeted property.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <typeparam name="TProp">The type of the property being targeted.</typeparam>
    /// <param name="document">The original document state, including its data and metadata.</param>
    /// <param name="propertyExpression">An expression pinpointing the target property for the explicit intent.</param>
    /// <param name="intent">An object representing the intent to perform (e.g., <see cref="InsertIntent"/>).</param>
    /// <returns>A <see cref="CrdtOperation"/> representing the explicit operation.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var doc = new CrdtDocument<MyDataObject>(new MyDataObject(), metadata);
    /// var intent = new SetIntent("Alice");
    /// 
    /// var operation = patcher.GenerateOperation(doc, x => x.Name, intent);
    /// ]]>
    /// </code>
    /// </example>
    CrdtOperation GenerateOperation<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent) where T : class;

    /// <summary>
    /// Generates a single CRDT operation explicitly based on a provided operation intent and a specific timestamp, bypassing the state diffing process.
    /// Uses an expression tree to strongly type and identify the targeted property.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <typeparam name="TProp">The type of the property being targeted.</typeparam>
    /// <param name="document">The original document state, including its data and metadata.</param>
    /// <param name="propertyExpression">An expression pinpointing the target property for the explicit intent.</param>
    /// <param name="intent">An object representing the intent to perform (e.g., <see cref="InsertIntent"/>).</param>
    /// <param name="timestamp">The specific timestamp to assign to the generated operation.</param>
    /// <returns>A <see cref="CrdtOperation"/> representing the explicit operation.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var doc = new CrdtDocument<MyDataObject>(new MyDataObject(), metadata);
    /// var intent = new SetIntent("Alice");
    /// var customTs = new EpochTimestamp(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    /// 
    /// var operation = patcher.GenerateOperation(doc, x => x.Name, intent, customTs);
    /// ]]>
    /// </code>
    /// </example>
    CrdtOperation GenerateOperation<T, TProp>([DisallowNull] CrdtDocument<T> document, Expression<Func<T, TProp>> propertyExpression, IOperationIntent intent, [DisallowNull] ICrdtTimestamp timestamp) where T : class;
}