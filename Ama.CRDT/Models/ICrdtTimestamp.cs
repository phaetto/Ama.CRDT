namespace Ama.CRDT.Models;

/// <summary>
/// Represents a logical point in time for a CRDT operation. Implementations must be comparable.
/// </summary>
public interface ICrdtTimestamp : IComparable<ICrdtTimestamp>
{
}