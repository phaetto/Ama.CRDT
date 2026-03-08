namespace Ama.CRDT.Models;

/// <summary>
/// Represents the status or failure reason of a CRDT operation during application.
/// </summary>
public enum CrdtOperationStatus : byte
{
    /// <summary>
    /// The operation was successfully applied.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The operation was ignored because it is obsolete according to the version vector.
    /// </summary>
    Obsolete = 1,

    /// <summary>
    /// The operation was ignored because it has already been seen (duplicate).
    /// </summary>
    Duplicate = 2,

    /// <summary>
    /// The operation failed because the target path could not be resolved.
    /// </summary>
    PathResolutionFailed = 3,

    /// <summary>
    /// The strategy failed to apply the operation.
    /// </summary>
    StrategyApplicationFailed = 4
}