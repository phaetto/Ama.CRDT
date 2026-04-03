namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for the SortedSetStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtAotType(typeof(SortedSetStrategyTests.TestModel))]
[CrdtAotType(typeof(SortedSetStrategyTests.MutableTestModel))]
[CrdtAotType(typeof(SortedSetStrategyTests.NestedModel))]
[CrdtAotType(typeof(SortedSetStrategyTests.ConvergenceTestModel))]
[CrdtAotType(typeof(SortedSetStrategyTests.SortTestModel))]
[CrdtAotType(typeof(SortedSetStrategyTests.Item))]
[CrdtAotType(typeof(SortedSetStrategyTests.TestUser))]
[CrdtAotType(typeof(List<SortedSetStrategyTests.NestedModel>))]
[CrdtAotType(typeof(List<string>))]
[CrdtAotType(typeof(List<SortedSetStrategyTests.Item>))]
[CrdtAotType(typeof(List<SortedSetStrategyTests.TestUser>))]
[CrdtAotType(typeof(string))]
internal partial class SortedSetStrategyTestCrdtAotContext : CrdtAotContext
{
}