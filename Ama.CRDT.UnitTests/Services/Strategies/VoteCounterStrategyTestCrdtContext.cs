namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for VoteCounterStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtSerializable(typeof(VoteCounterStrategyTests.Poll))]
[CrdtSerializable(typeof(VoteCounterStrategyTests.PollWithList))]
[CrdtSerializable(typeof(Dictionary<string, HashSet<string>>))]
[CrdtSerializable(typeof(Dictionary<string, List<string>>))]
[CrdtSerializable(typeof(HashSet<string>))]
[CrdtSerializable(typeof(List<string>))]
internal partial class VoteCounterStrategyTestCrdtContext : CrdtContext
{
}