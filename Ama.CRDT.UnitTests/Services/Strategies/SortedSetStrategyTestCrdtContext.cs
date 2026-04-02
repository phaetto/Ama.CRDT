namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for the SortedSetStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtSerializable(typeof(SortedSetStrategyTests.TestModel))]
[CrdtSerializable(typeof(SortedSetStrategyTests.MutableTestModel))]
[CrdtSerializable(typeof(SortedSetStrategyTests.NestedModel))]
[CrdtSerializable(typeof(SortedSetStrategyTests.ConvergenceTestModel))]
[CrdtSerializable(typeof(SortedSetStrategyTests.SortTestModel))]
[CrdtSerializable(typeof(SortedSetStrategyTests.Item))]
[CrdtSerializable(typeof(SortedSetStrategyTests.TestUser))]
[CrdtSerializable(typeof(List<SortedSetStrategyTests.NestedModel>))]
[CrdtSerializable(typeof(List<string>))]
[CrdtSerializable(typeof(List<SortedSetStrategyTests.Item>))]
[CrdtSerializable(typeof(List<SortedSetStrategyTests.TestUser>))]
[CrdtSerializable(typeof(string))]
internal partial class SortedSetStrategyTestCrdtContext : CrdtContext
{
}