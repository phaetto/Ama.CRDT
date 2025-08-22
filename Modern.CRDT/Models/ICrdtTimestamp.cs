namespace Modern.CRDT.Models;

/// <summary>
/// Represents a logical point in time for a CRDT operation, allowing for different timestamping mechanisms.
/// </summary>
public interface ICrdtTimestamp : IComparable<ICrdtTimestamp>
{
}