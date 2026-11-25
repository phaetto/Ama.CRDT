namespace Ama.CRDT.Extensions;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Serialization;

/// <summary>
/// Provides extension methods for working with <see cref="PackedCrdtDocument{T}"/> across CRDT services,
/// allowing seamless packing, unpacking, patch generation, application, and merging.
/// </summary>
public static class PackedCrdtDocumentExtensions
{
    /// <summary>
    /// Packs a <see cref="CrdtDocument{T}"/> into a <see cref="PackedCrdtDocument{T}"/> by serializing its metadata to a byte array.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="document">The unpacked CRDT document.</param>
    /// <param name="serializer">The serializer used to encode the metadata.</param>
    /// <returns>A <see cref="PackedCrdtDocument{T}"/> containing the data model and serialized metadata bytes.</returns>
    public static PackedCrdtDocument<T> Pack<T>(this CrdtDocument<T> document, [DisallowNull] ICrdtSerializer serializer) where T : class
    {
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(document.Metadata);

        var bytes = serializer.SerializeToBytes(document.Metadata);
        return new PackedCrdtDocument<T>(document.Data, bytes);
    }

    /// <summary>
    /// Unpacks a <see cref="PackedCrdtDocument{T}"/> into a <see cref="CrdtDocument{T}"/> by deserializing its metadata bytes.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="packedDocument">The packed CRDT document.</param>
    /// <param name="serializer">The serializer used to decode the metadata.</param>
    /// <returns>An unpacked <see cref="CrdtDocument{T}"/> with materialized <see cref="CrdtMetadata"/>.</returns>
    public static CrdtDocument<T> Unpack<T>(this PackedCrdtDocument<T> packedDocument, [DisallowNull] ICrdtSerializer serializer) where T : class
    {
        ArgumentNullException.ThrowIfNull(serializer);

        if (packedDocument.MetadataBytes.IsEmpty)
        {
            return new CrdtDocument<T>(packedDocument.Data, new CrdtMetadata());
        }

        var metadata = serializer.DeserializeFromBytes<CrdtMetadata>(packedDocument.MetadataBytes.Span) ?? new CrdtMetadata();
        return new CrdtDocument<T>(packedDocument.Data, metadata);
    }

    /// <summary>
    /// Unpacks the <see cref="PackedCrdtDocument{T}"/>, applies the CRDT patch, and repacks the updated metadata.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="applicator">The synchronous CRDT applicator.</param>
    /// <param name="packedDocument">The packed CRDT document to apply the patch to.</param>
    /// <param name="patch">The CRDT patch to apply.</param>
    /// <param name="serializer">The serializer used to unpack and repack the metadata.</param>
    /// <returns>A <see cref="PackedApplyPatchResult{T}"/> containing the updated <see cref="PackedCrdtDocument{T}"/> and any unapplied operations.</returns>
    public static PackedApplyPatchResult<T> ApplyPatch<T>(
        [DisallowNull] this ICrdtApplicator applicator,
        PackedCrdtDocument<T> packedDocument,
        CrdtPatch patch,
        [DisallowNull] ICrdtSerializer serializer) where T : class
    {
        ArgumentNullException.ThrowIfNull(applicator);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(packedDocument.Data);

        var unpacked = packedDocument.Unpack(serializer);
        var result = applicator.ApplyPatch(unpacked, patch);
        var repacked = result.Document.Pack(serializer);

        return new PackedApplyPatchResult<T>(repacked, result.UnappliedOperations);
    }

    /// <summary>
    /// Asynchronously unpacks the <see cref="PackedCrdtDocument{T}"/>, applies the CRDT patch through the async applicator pipeline, and repacks the updated metadata.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="applicator">The asynchronous CRDT applicator.</param>
    /// <param name="packedDocument">The packed CRDT document to apply the patch to.</param>
    /// <param name="patch">The CRDT patch to apply.</param>
    /// <param name="serializer">The serializer used to unpack and repack the metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing a <see cref="PackedApplyPatchResult{T}"/>.</returns>
    public static async Task<PackedApplyPatchResult<T>> ApplyPatchAsync<T>(
        [DisallowNull] this IAsyncCrdtApplicator applicator,
        PackedCrdtDocument<T> packedDocument,
        CrdtPatch patch,
        [DisallowNull] ICrdtSerializer serializer,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(applicator);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(packedDocument.Data);

        var unpacked = packedDocument.Unpack(serializer);
        var result = await applicator.ApplyPatchAsync(unpacked, patch, cancellationToken).ConfigureAwait(false);
        var repacked = result.Document.Pack(serializer);

        return new PackedApplyPatchResult<T>(repacked, result.UnappliedOperations);
    }

    /// <summary>
    /// Unpacks the <see cref="PackedCrdtDocument{T}"/> and generates a CRDT patch by comparing it to the modified state.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="patcher">The synchronous CRDT patcher.</param>
    /// <param name="from">The original packed document state.</param>
    /// <param name="changed">The modified document data.</param>
    /// <param name="serializer">The serializer used to unpack the metadata.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the generated operations.</returns>
    public static CrdtPatch GeneratePatch<T>(
        [DisallowNull] this ICrdtPatcher patcher,
        PackedCrdtDocument<T> from,
        [DisallowNull] T changed,
        [DisallowNull] ICrdtSerializer serializer) where T : class
    {
        ArgumentNullException.ThrowIfNull(patcher);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(serializer);

        var unpacked = from.Unpack(serializer);
        return patcher.GeneratePatch(unpacked, changed);
    }

    /// <summary>
    /// Unpacks the <see cref="PackedCrdtDocument{T}"/> and generates a CRDT patch with a specific timestamp by comparing it to the modified state.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="patcher">The synchronous CRDT patcher.</param>
    /// <param name="from">The original packed document state.</param>
    /// <param name="changed">The modified document data.</param>
    /// <param name="changeTimestamp">The timestamp to assign to generated operations.</param>
    /// <param name="serializer">The serializer used to unpack the metadata.</param>
    /// <returns>A <see cref="CrdtPatch"/> containing the generated operations.</returns>
    public static CrdtPatch GeneratePatch<T>(
        [DisallowNull] this ICrdtPatcher patcher,
        PackedCrdtDocument<T> from,
        [DisallowNull] T changed,
        CrdtTimestamp changeTimestamp,
        [DisallowNull] ICrdtSerializer serializer) where T : class
    {
        ArgumentNullException.ThrowIfNull(patcher);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(serializer);

        var unpacked = from.Unpack(serializer);
        return patcher.GeneratePatch(unpacked, changed, changeTimestamp);
    }

    /// <summary>
    /// Unpacks the <see cref="PackedCrdtDocument{T}"/> and generates a single CRDT operation explicitly based on an intent.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <typeparam name="TProp">The type of the targeted property.</typeparam>
    /// <param name="patcher">The synchronous CRDT patcher.</param>
    /// <param name="document">The packed document state.</param>
    /// <param name="propertyExpression">An expression identifying the target property.</param>
    /// <param name="intent">The explicit operation intent.</param>
    /// <param name="serializer">The serializer used to unpack the metadata.</param>
    /// <returns>The generated <see cref="CrdtOperation"/>.</returns>
    public static CrdtOperation GenerateOperation<T, TProp>(
        [DisallowNull] this ICrdtPatcher patcher,
        PackedCrdtDocument<T> document,
        [DisallowNull] Expression<Func<T, TProp>> propertyExpression,
        [DisallowNull] IOperationIntent intent,
        [DisallowNull] ICrdtSerializer serializer) where T : class
    {
        ArgumentNullException.ThrowIfNull(patcher);
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(propertyExpression);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(serializer);

        var unpacked = document.Unpack(serializer);
        return patcher.GenerateOperation(unpacked, propertyExpression, intent);
    }

    /// <summary>
    /// Unpacks the <see cref="PackedCrdtDocument{T}"/> and generates a single CRDT operation explicitly based on an intent and a specific timestamp.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <typeparam name="TProp">The type of the targeted property.</typeparam>
    /// <param name="patcher">The synchronous CRDT patcher.</param>
    /// <param name="document">The packed document state.</param>
    /// <param name="propertyExpression">An expression identifying the target property.</param>
    /// <param name="intent">The explicit operation intent.</param>
    /// <param name="timestamp">The timestamp to assign to the operation.</param>
    /// <param name="serializer">The serializer used to unpack the metadata.</param>
    /// <returns>The generated <see cref="CrdtOperation"/>.</returns>
    public static CrdtOperation GenerateOperation<T, TProp>(
        [DisallowNull] this ICrdtPatcher patcher,
        PackedCrdtDocument<T> document,
        [DisallowNull] Expression<Func<T, TProp>> propertyExpression,
        [DisallowNull] IOperationIntent intent,
        CrdtTimestamp timestamp,
        [DisallowNull] ICrdtSerializer serializer) where T : class
    {
        ArgumentNullException.ThrowIfNull(patcher);
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(propertyExpression);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(serializer);

        var unpacked = document.Unpack(serializer);
        return patcher.GenerateOperation(unpacked, propertyExpression, intent, timestamp);
    }

    /// <summary>
    /// Asynchronously unpacks the <see cref="PackedCrdtDocument{T}"/> and generates a CRDT patch by comparing it to the modified state.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="patcher">The asynchronous CRDT patcher.</param>
    /// <param name="from">The original packed document state.</param>
    /// <param name="changed">The modified document data.</param>
    /// <param name="serializer">The serializer used to unpack the metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the generated <see cref="CrdtPatch"/>.</returns>
    public static Task<CrdtPatch> GeneratePatchAsync<T>(
        [DisallowNull] this IAsyncCrdtPatcher patcher,
        PackedCrdtDocument<T> from,
        [DisallowNull] T changed,
        [DisallowNull] ICrdtSerializer serializer,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(patcher);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(serializer);

        var unpacked = from.Unpack(serializer);
        return patcher.GeneratePatchAsync(unpacked, changed, cancellationToken);
    }

    /// <summary>
    /// Asynchronously unpacks the <see cref="PackedCrdtDocument{T}"/> and generates a CRDT patch with a specific timestamp by comparing it to the modified state.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="patcher">The asynchronous CRDT patcher.</param>
    /// <param name="from">The original packed document state.</param>
    /// <param name="changed">The modified document data.</param>
    /// <param name="changeTimestamp">The timestamp to assign to generated operations.</param>
    /// <param name="serializer">The serializer used to unpack the metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the generated <see cref="CrdtPatch"/>.</returns>
    public static Task<CrdtPatch> GeneratePatchAsync<T>(
        [DisallowNull] this IAsyncCrdtPatcher patcher,
        PackedCrdtDocument<T> from,
        [DisallowNull] T changed,
        CrdtTimestamp changeTimestamp,
        [DisallowNull] ICrdtSerializer serializer,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(patcher);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(serializer);

        var unpacked = from.Unpack(serializer);
        return patcher.GeneratePatchAsync(unpacked, changed, changeTimestamp, cancellationToken);
    }

    /// <summary>
    /// Asynchronously unpacks the <see cref="PackedCrdtDocument{T}"/> and generates a single CRDT operation explicitly based on an intent.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <typeparam name="TProp">The type of the targeted property.</typeparam>
    /// <param name="patcher">The asynchronous CRDT patcher.</param>
    /// <param name="document">The packed document state.</param>
    /// <param name="propertyExpression">An expression identifying the target property.</param>
    /// <param name="intent">The explicit operation intent.</param>
    /// <param name="serializer">The serializer used to unpack the metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the generated <see cref="CrdtOperation"/>.</returns>
    public static Task<CrdtOperation> GenerateOperationAsync<T, TProp>(
        [DisallowNull] this IAsyncCrdtPatcher patcher,
        PackedCrdtDocument<T> document,
        [DisallowNull] Expression<Func<T, TProp>> propertyExpression,
        [DisallowNull] IOperationIntent intent,
        [DisallowNull] ICrdtSerializer serializer,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(patcher);
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(propertyExpression);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(serializer);

        var unpacked = document.Unpack(serializer);
        return patcher.GenerateOperationAsync(unpacked, propertyExpression, intent, cancellationToken);
    }

    /// <summary>
    /// Asynchronously unpacks the <see cref="PackedCrdtDocument{T}"/> and generates a single CRDT operation explicitly based on an intent and a specific timestamp.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <typeparam name="TProp">The type of the targeted property.</typeparam>
    /// <param name="patcher">The asynchronous CRDT patcher.</param>
    /// <param name="document">The packed document state.</param>
    /// <param name="propertyExpression">An expression identifying the target property.</param>
    /// <param name="intent">The explicit operation intent.</param>
    /// <param name="timestamp">The timestamp to assign to the operation.</param>
    /// <param name="serializer">The serializer used to unpack the metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the generated <see cref="CrdtOperation"/>.</returns>
    public static Task<CrdtOperation> GenerateOperationAsync<T, TProp>(
        [DisallowNull] this IAsyncCrdtPatcher patcher,
        PackedCrdtDocument<T> document,
        [DisallowNull] Expression<Func<T, TProp>> propertyExpression,
        [DisallowNull] IOperationIntent intent,
        CrdtTimestamp timestamp,
        [DisallowNull] ICrdtSerializer serializer,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(patcher);
        ArgumentNullException.ThrowIfNull(document.Data);
        ArgumentNullException.ThrowIfNull(propertyExpression);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(serializer);

        var unpacked = document.Unpack(serializer);
        return patcher.GenerateOperationAsync(unpacked, propertyExpression, intent, timestamp, cancellationToken);
    }

    /// <summary>
    /// Unpacks both <see cref="PackedCrdtDocument{T}"/> instances, merges the secondary document into the primary document, and returns the repacked primary document.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="merger">The synchronous CRDT merger.</param>
    /// <param name="primary">The primary packed document state.</param>
    /// <param name="secondary">The secondary packed document state to merge into primary.</param>
    /// <param name="serializer">The serializer used to unpack and repack the metadata.</param>
    /// <returns>A new <see cref="PackedCrdtDocument{T}"/> containing the merged primary data and newly packed metadata.</returns>
    public static PackedCrdtDocument<T> MergeState<T>(
        [DisallowNull] this ICrdtMerger merger,
        PackedCrdtDocument<T> primary,
        PackedCrdtDocument<T> secondary,
        [DisallowNull] ICrdtSerializer serializer) where T : class
    {
        ArgumentNullException.ThrowIfNull(merger);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(primary.Data);
        ArgumentNullException.ThrowIfNull(secondary.Data);

        var unpackedPrimary = primary.Unpack(serializer);
        var unpackedSecondary = secondary.Unpack(serializer);

        merger.MergeState(unpackedPrimary, unpackedSecondary);

        return unpackedPrimary.Pack(serializer);
    }

    /// <summary>
    /// Asynchronously unpacks both <see cref="PackedCrdtDocument{T}"/> instances, merges the secondary document into the primary document, and returns the repacked primary document.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="merger">The asynchronous CRDT merger.</param>
    /// <param name="primary">The primary packed document state.</param>
    /// <param name="secondary">The secondary packed document state to merge into primary.</param>
    /// <param name="serializer">The serializer used to unpack and repack the metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the merged <see cref="PackedCrdtDocument{T}"/>.</returns>
    public static async Task<PackedCrdtDocument<T>> MergeStateAsync<T>(
        [DisallowNull] this IAsyncCrdtMerger merger,
        PackedCrdtDocument<T> primary,
        PackedCrdtDocument<T> secondary,
        [DisallowNull] ICrdtSerializer serializer,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(merger);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(primary.Data);
        ArgumentNullException.ThrowIfNull(secondary.Data);

        var unpackedPrimary = primary.Unpack(serializer);
        var unpackedSecondary = secondary.Unpack(serializer);

        await merger.MergeStateAsync(unpackedPrimary, unpackedSecondary, cancellationToken).ConfigureAwait(false);

        return unpackedPrimary.Pack(serializer);
    }

    /// <summary>
    /// Unpacks the <see cref="PackedCrdtDocument{T}"/>, compacts its metadata using the specified policy, and repacks the compacted metadata.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="metadataManager">The CRDT metadata manager.</param>
    /// <param name="packedDocument">The packed document to compact.</param>
    /// <param name="policy">The compaction policy determining which tombstones and metadata entries can be safely pruned.</param>
    /// <returns>A new <see cref="PackedCrdtDocument{T}"/> with compacted and repacked metadata.</returns>
    public static PackedCrdtDocument<T> Compact<T>(
        [DisallowNull] this ICrdtMetadataManager metadataManager,
        PackedCrdtDocument<T> packedDocument,
        [DisallowNull] ICompactionPolicy policy) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadataManager);
        ArgumentNullException.ThrowIfNull(packedDocument.Data);
        ArgumentNullException.ThrowIfNull(policy);

        var unpacked = metadataManager.Unpack(packedDocument);
        metadataManager.Compact(unpacked, policy);
        return metadataManager.Pack(unpacked);
    }

    /// <summary>
    /// Unpacks the <see cref="PackedCrdtDocument{T}"/>, evicts the specified replica from its causal tracking history, and repacks the updated metadata.
    /// </summary>
    /// <typeparam name="T">The type of the data model.</typeparam>
    /// <param name="metadataManager">The CRDT metadata manager.</param>
    /// <param name="packedDocument">The packed document to update.</param>
    /// <param name="replicaId">The identifier of the permanently evicted replica.</param>
    /// <returns>A new <see cref="PackedCrdtDocument{T}"/> with the replica evicted from its packed metadata.</returns>
    public static PackedCrdtDocument<T> EvictReplica<T>(
        [DisallowNull] this ICrdtMetadataManager metadataManager,
        PackedCrdtDocument<T> packedDocument,
        [DisallowNull] string replicaId) where T : class
    {
        ArgumentNullException.ThrowIfNull(metadataManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);

        var unpacked = metadataManager.Unpack(packedDocument);
        metadataManager.EvictReplica(unpacked, replicaId);
        return metadataManager.Pack(unpacked);
    }
}