namespace Ama.CRDT.Services.Versioning;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;

/// <summary>
/// Provides utilities to compare the causality state (Dotted Version Vectors) of different replicas and determine synchronization requirements.
/// </summary>
public interface IVersionVectorSyncService
{
    /// <summary>
    /// Compares two replica contexts and determines what data the target replica needs from the source replica.
    /// </summary>
    /// <param name="target">The context of the replica that may be behind.</param>
    /// <param name="source">The context of the replica that may have newer data.</param>
    /// <returns>A <see cref="ReplicaSyncRequirement"/> detailing exactly what versions the target is missing from the source.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var targetContext = new ReplicaContext 
    /// { 
    ///     ReplicaId = "ReplicaA",
    ///     GlobalVersionVector = new DottedVersionVector()
    /// };
    /// targetContext.GlobalVersionVector.Add("OriginX", 1);
    /// 
    /// var sourceContext = new ReplicaContext 
    /// { 
    ///     ReplicaId = "ReplicaB",
    ///     GlobalVersionVector = new DottedVersionVector()
    /// };
    /// sourceContext.GlobalVersionVector.Add("OriginX", 1);
    /// sourceContext.GlobalVersionVector.Add("OriginX", 2);
    /// sourceContext.GlobalVersionVector.Add("OriginX", 3);
    /// 
    /// var syncService = new VersionVectorSyncService();
    /// var requirements = syncService.CalculateRequirement(targetContext, sourceContext);
    /// 
    /// if (requirements.IsBehind)
    /// {
    ///     var xReq = requirements.RequirementsByOrigin["OriginX"];
    ///     // xReq.TargetContiguousVersion will be 1
    ///     // xReq.SourceContiguousVersion will be 3
    ///     // This means ReplicaA needs operations 2 and 3 from OriginX.
    /// }
    /// ]]>
    /// </code>
    /// </example>
    ReplicaSyncRequirement CalculateRequirement(ReplicaContext target, ReplicaContext source);

    /// <summary>
    /// Compares two dotted version vectors and determines what data the target replica needs from the source replica.
    /// </summary>
    /// <param name="targetReplicaId">The ID of the replica that may be behind.</param>
    /// <param name="targetVector">The version vector of the replica that may be behind.</param>
    /// <param name="sourceReplicaId">The ID of the replica that may have newer data.</param>
    /// <param name="sourceVector">The version vector of the replica that may have newer data.</param>
    /// <returns>A <see cref="ReplicaSyncRequirement"/> detailing exactly what versions the target is missing from the source.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var targetVector = new DottedVersionVector();
    /// targetVector.Add("OriginX", 1);
    /// 
    /// var sourceVector = new DottedVersionVector();
    /// sourceVector.Add("OriginX", 3);
    /// 
    /// var syncService = new VersionVectorSyncService();
    /// var requirements = syncService.CalculateRequirement("ReplicaA", targetVector, "ReplicaB", sourceVector);
    /// 
    /// if (requirements.IsBehind)
    /// {
    ///     // Handle synchronization from ReplicaB to ReplicaA
    /// }
    /// ]]>
    /// </code>
    /// </example>
    ReplicaSyncRequirement CalculateRequirement(string targetReplicaId, DottedVersionVector targetVector, string sourceReplicaId, DottedVersionVector sourceVector);

    /// <summary>
    /// Compares two replica contexts bidirectionally, returning the requirements for both to fully synchronize with each other.
    /// </summary>
    /// <param name="replicaA">The first replica context.</param>
    /// <param name="replicaB">The second replica context.</param>
    /// <returns>A <see cref="BidirectionalSyncRequirements"/> containing the requirements for A to catch up to B, and B to catch up to A.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var syncService = new VersionVectorSyncService();
    /// var requirements = syncService.CalculateBidirectionalRequirements(replicaA, replicaB);
    /// 
    /// if (requirements.ReplicaANeedsFromB.IsBehind)
    /// {
    ///     // Request data from B to A
    /// }
    /// 
    /// if (requirements.ReplicaBNeedsFromA.IsBehind)
    /// {
    ///     // Request data from A to B
    /// }
    /// ]]>
    /// </code>
    /// </example>
    BidirectionalSyncRequirements CalculateBidirectionalRequirements(ReplicaContext replicaA, ReplicaContext replicaB);

    /// <summary>
    /// Compares two dotted version vectors bidirectionally, returning the requirements for both to fully synchronize with each other.
    /// </summary>
    /// <param name="replicaAId">The ID of the first replica.</param>
    /// <param name="vectorA">The version vector of the first replica.</param>
    /// <param name="replicaBId">The ID of the second replica.</param>
    /// <param name="vectorB">The version vector of the second replica.</param>
    /// <returns>A <see cref="BidirectionalSyncRequirements"/> containing the requirements for A to catch up to B, and B to catch up to A.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var vectorA = new DottedVersionVector();
    /// vectorA.Add("OriginX", 2);
    /// 
    /// var vectorB = new DottedVersionVector();
    /// vectorB.Add("OriginY", 3);
    /// 
    /// var syncService = new VersionVectorSyncService();
    /// var requirements = syncService.CalculateBidirectionalRequirements("ReplicaA", vectorA, "ReplicaB", vectorB);
    /// 
    /// if (requirements.ReplicaANeedsFromB.IsBehind)
    /// {
    ///     // Sync needed for A
    /// }
    /// 
    /// if (requirements.ReplicaBNeedsFromA.IsBehind)
    /// {
    ///     // Sync needed for B
    /// }
    /// ]]>
    /// </code>
    /// </example>
    BidirectionalSyncRequirements CalculateBidirectionalRequirements(string replicaAId, DottedVersionVector vectorA, string replicaBId, DottedVersionVector vectorB);

    /// <summary>
    /// Calculates the Global Minimum Version Vector from a collection of replica version vectors.
    /// </summary>
    /// <param name="clusterVectors">The version vectors of all active replicas in the cluster.</param>
    /// <returns>A dictionary mapping each origin replica ID to the lowest contiguous version acknowledged by all replicas in the cluster.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var vector1 = new DottedVersionVector();
    /// vector1.Add("OriginX", 5);
    /// 
    /// var vector2 = new DottedVersionVector();
    /// vector2.Add("OriginX", 4);
    /// vector2.Add("OriginY", 2);
    /// 
    /// var vector3 = new DottedVersionVector();
    /// vector3.Add("OriginX", 6);
    /// 
    /// var syncService = new VersionVectorSyncService();
    /// var clusterVectors = new[] { vector1, vector2, vector3 };
    /// var gmvv = syncService.CalculateGlobalMinimumVersionVector(clusterVectors);
    /// 
    /// // gmvv["OriginX"] is 4 (the minimum contiguous version known by ALL replicas)
    /// // gmvv does not contain "OriginY" (since vector1 and vector3 have not seen it)
    /// ]]>
    /// </code>
    /// </example>
    IReadOnlyDictionary<string, long> CalculateGlobalMinimumVersionVector(IEnumerable<DottedVersionVector> clusterVectors);

    /// <summary>
    /// Calculates the Global Maximum Version Vector from a collection of replica version vectors, 
    /// representing the absolute latest known causal state across the entire cluster.
    /// </summary>
    /// <param name="clusterVectors">The version vectors of all active replicas in the cluster.</param>
    /// <returns>A new <see cref="DottedVersionVector"/> merging all contiguous versions and dots.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var vector1 = new DottedVersionVector();
    /// vector1.Add("OriginX", 2);
    /// 
    /// var vector2 = new DottedVersionVector();
    /// vector2.Add("OriginX", 5);
    /// 
    /// var syncService = new VersionVectorSyncService();
    /// var clusterVectors = new[] { vector1, vector2 };
    /// var maximumState = syncService.CalculateGlobalMaximumVersionVector(clusterVectors);
    /// 
    /// // maximumState will reflect "OriginX" = 5
    /// ]]>
    /// </code>
    /// </example>
    DottedVersionVector CalculateGlobalMaximumVersionVector(IEnumerable<DottedVersionVector> clusterVectors);

    /// <summary>
    /// Removes causal tracking information for specific replicas from a given version vector.
    /// This is useful for cleaning up version vectors after replicas have been evicted from the cluster,
    /// allowing the Global Minimum Version Vector (GMVV) to advance.
    /// </summary>
    /// <param name="vector">The version vector to clean.</param>
    /// <param name="evictedReplicaIds">The IDs of the replicas to remove.</param>
    /// <returns>A new <see cref="DottedVersionVector"/> without the evicted replicas.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var vector = new DottedVersionVector();
    /// vector.Add("ReplicaA", 5);
    /// vector.Add("ReplicaB", 3);
    /// 
    /// var syncService = new VersionVectorSyncService();
    /// var cleanVector = syncService.RemoveEvictedReplicas(vector, new[] { "ReplicaB" });
    /// 
    /// // cleanVector will only contain "ReplicaA"
    /// ]]>
    /// </code>
    /// </example>
    DottedVersionVector RemoveEvictedReplicas(DottedVersionVector vector, IEnumerable<string> evictedReplicaIds);

    /// <summary>
    /// Evaluates if an asynchronous stream of retrieved operations fully satisfies the synchronization requirement, 
    /// or if the stream is missing required causal gaps (indicating truncation and a need for a full state snapshot).
    /// </summary>
    /// <param name="retrievedOperations">The asynchronous stream of operations retrieved from a source (e.g., a journal).</param>
    /// <param name="requirement">The synchronization requirement detailing the missing causal versions.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="JournalSyncResult"/> containing the materialized operations and a flag indicating if a snapshot is required to bridge gaps.</returns>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// var requirement = syncService.CalculateRequirement(targetContext, sourceContext);
    /// var operationStream = operationJournal.GetOperationsAsync(requirement);
    /// 
    /// var result = await syncService.EvaluateJournalCompletionAsync(operationStream, requirement);
    /// 
    /// if (result.RequiresSnapshot)
    /// {
    ///     // Request full snapshot from source because the journal was truncated
    ///     // and could not supply all the missing operations needed to catch up.
    /// }
    /// else
    /// {
    ///     // Apply result.Operations safely to close the gap
    /// }
    /// ]]>
    /// </code>
    /// </example>
    Task<JournalSyncResult> EvaluateJournalCompletionAsync(IAsyncEnumerable<JournaledOperation> retrievedOperations, ReplicaSyncRequirement requirement, CancellationToken cancellationToken = default);
}