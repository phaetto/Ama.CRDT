namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for VoteCounterStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtAotType(typeof(VoteCounterStrategyTests.Poll))]
[CrdtAotType(typeof(VoteCounterStrategyTests.PollWithList))]
[CrdtAotType(typeof(Dictionary<string, HashSet<string>>))]
[CrdtAotType(typeof(Dictionary<string, List<string>>))]
[CrdtAotType(typeof(HashSet<string>))]
[CrdtAotType(typeof(List<string>))]
[CrdtAotType(typeof(IDictionary<string, HashSet<string>>))]
[CrdtAotType(typeof(IDictionary<string, List<string>>))]
internal partial class VoteCounterStrategyTestCrdtAotContext : CrdtAotContext
{
}