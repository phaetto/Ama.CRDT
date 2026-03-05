namespace Ama.CRDT.Models.Decorators;

using System;

/// <summary>
/// A payload structure used by the Epoch Bound strategy to wrap underlying operations with a generation counter.
/// </summary>
/// <param name="Epoch">The epoch generation this operation belongs to.</param>
/// <param name="Value">The underlying operation payload.</param>
public readonly record struct EpochPayload(int Epoch, object? Value) : IEquatable<EpochPayload>;