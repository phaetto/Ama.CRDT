namespace Ama.CRDT.Models.Intents;

/// <summary>
/// A marker interface for explicitly representing a user's action or intent, 
/// to be translated directly into CRDT operations without state-based diffing.
/// </summary>
public interface IOperationIntent
{
}