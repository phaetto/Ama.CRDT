namespace Ama.CRDT.Models;

/// <summary>
/// Defines the structural execution phase of a CRDT pipeline decorator.
/// </summary>
/// <remarks>
/// When multiple decorators are chained together, they form a pipeline wrapping the core component.
/// Understanding the execution order is crucial when combining multiple behaviors:
/// <list type="bullet">
/// <item><description><see cref="Before"/>: Executes outside-in. The outermost decorator executes its logic first, drilling down to the innermost decorator, and finally reaching the core component.</description></item>
/// <item><description><see cref="After"/>: Executes inside-out. The core component executes first. As the execution stack unwinds, the innermost decorator's logic executes, moving outward so the outermost decorator's logic runs last.</description></item>
/// <item><description><see cref="Complex"/>: Replaces or entirely wraps the execution flow, capable of executing logic both before and after the inner component, or bypassing it completely.</description></item>
/// </list>
/// </remarks>
public enum DecoratorBehavior
{
    /// <summary>
    /// The decorator executes its custom logic strictly before delegating to the inner component.
    /// In a nested chain, this executes from the outermost wrapper down to the innermost (first to last before the core).
    /// </summary>
    Before,

    /// <summary>
    /// The decorator executes its custom logic strictly after the inner component has finished successfully.
    /// In a nested chain, this executes from the innermost wrapper up to the outermost (last to first as the stack unwinds).
    /// </summary>
    After,

    /// <summary>
    /// The decorator entirely controls the execution flow, including whether to call the inner component multiple times, catch exceptions, or not call it at all.
    /// </summary>
    Complex
}