namespace Ama.CRDT.Models;

using System;

/// <summary>
/// Represents a CRDT operation that could not be applied and the reason why.
/// </summary>
/// <param name="Operation">The CRDT operation that was not applied.</param>
/// <param name="Reason">The reason the operation was not applied.</param>
public readonly record struct UnappliedOperation(
    CrdtOperation Operation, 
    CrdtOperationStatus Reason) : IEquatable<UnappliedOperation>;