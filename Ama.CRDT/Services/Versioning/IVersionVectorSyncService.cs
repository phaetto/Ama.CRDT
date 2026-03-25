namespace Ama.CRDT.Services.Versioning;

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
}