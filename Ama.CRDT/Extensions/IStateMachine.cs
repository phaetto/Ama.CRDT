namespace Ama.CRDT.Extensions;

/// <summary>
/// Defines the contract for a state machine that validates transitions between states.
/// </summary>
/// <typeparam name="TState">The type of the state (e.g., string, enum).</typeparam>
public interface IStateMachine<TState>
{
    /// <summary>
    /// Determines if a transition from a given state to another is valid.
    /// </summary>
    /// <param name="from">The current state. Can be the default value if the property is being initialized.</param>
    /// <param name="to">The target state.</param>
    /// <returns><c>true</c> if the transition is valid; otherwise, <c>false</c>.</returns>
    bool IsValidTransition(TState from, TState to);
}