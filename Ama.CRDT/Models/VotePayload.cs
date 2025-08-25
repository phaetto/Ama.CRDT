namespace Ama.CRDT.Models;

/// <summary>
/// Represents the payload for a vote operation, containing the identifier for the voter and their selected option.
/// </summary>
/// <param name="Voter">The unique identifier for the voter (e.g., a user ID).</param>
/// <param name="Option">The identifier for the option being voted for.</param>
public readonly record struct VotePayload(object Voter, object Option);