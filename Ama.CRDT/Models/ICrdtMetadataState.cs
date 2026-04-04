namespace Ama.CRDT.Models;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Represents the polymorphic base interface for all CRDT metadata state structures.
/// This allows mapping property JSON Paths to an abstracted state implementation.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CausalTimestamp), "causal-ts")]
[JsonDerivedType(typeof(FwwTimestamp), "fww-ts")]
[JsonDerivedType(typeof(PnCounterState), "pn-counter-state")]
[JsonDerivedType(typeof(LwwSetState), "lww-set-state")]
[JsonDerivedType(typeof(FwwSetState), "fww-set-state")]
[JsonDerivedType(typeof(OrSetState), "or-set-state")]
[JsonDerivedType(typeof(TwoPhaseSetState), "2p-set-state")]
[JsonDerivedType(typeof(TwoPhaseGraphState), "2p-graph-state")]
[JsonDerivedType(typeof(AverageRegisterState), "avg-reg-state")]
[JsonDerivedType(typeof(PositionalState), "pos-state")]
[JsonDerivedType(typeof(LseqState), "lseq-state")]
[JsonDerivedType(typeof(RgaState), "rga-state")]
[JsonDerivedType(typeof(LwwMapState), "lww-map-state")]
[JsonDerivedType(typeof(FwwMapState), "fww-map-state")]
[JsonDerivedType(typeof(CounterMapState), "counter-map-state")]
[JsonDerivedType(typeof(EpochState), "epoch-state")]
[JsonDerivedType(typeof(QuorumState), "quorum-state")]
public interface ICrdtMetadataState : IEquatable<ICrdtMetadataState>
{
    /// <summary>
    /// Creates a deep copy of the state to prevent unintended reference mutations.
    /// </summary>
    /// <returns>A cloned instance of the state.</returns>
    ICrdtMetadataState DeepClone();

    /// <summary>
    /// Merges the state with another state of the same type.
    /// </summary>
    /// <param name="other">The other state to merge with.</param>
    /// <returns>A new state containing the merged result.</returns>
    ICrdtMetadataState Merge(ICrdtMetadataState other);
}