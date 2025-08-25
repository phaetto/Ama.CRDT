namespace Ama.CRDT.Models;

/// <summary>
/// Represents a value contributed by a replica for an Average Register, including the timestamp of the contribution.
/// </summary>
/// <param name="Value">The numeric value of the contribution.</param>
/// <param name="Timestamp">The logical timestamp of when the contribution was made.</param>
public readonly record struct AverageRegisterValue(decimal Value, ICrdtTimestamp Timestamp);