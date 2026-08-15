namespace Ama.CRDT.Models;

using System;

/// <summary>
/// Represents an envelope for a <see cref="CrdtOperation"/> that includes the logical 
/// identity (Document ID) of the document it belongs to. This is used by the journal 
/// to properly route operations back to their respective partitions during synchronization.
/// </summary>
/// <param name="DocumentId">The logical key or ID of the root document.</param>
/// <param name="Operation">The CRDT operation.</param>
public readonly record struct JournaledOperation(IComparable DocumentId, CrdtOperation Operation);