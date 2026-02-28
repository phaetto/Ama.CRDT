namespace Ama.CRDT.Models.Intents;

/// <summary>
/// Represents the intent to explicitly cast a vote for a specific option.
/// </summary>
/// <param name="Voter">The identifier of the voter.</param>
/// <param name="Option">The option the voter is voting for.</param>
public readonly record struct VoteIntent(object Voter, object Option) : IOperationIntent;