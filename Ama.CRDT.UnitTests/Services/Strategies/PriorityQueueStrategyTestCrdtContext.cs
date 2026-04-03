namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for PriorityQueueStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtAotType(typeof(PriorityQueueStrategyTests.TestModel))]
[CrdtAotType(typeof(PriorityQueueStrategyTests.Item))]
[CrdtAotType(typeof(List<PriorityQueueStrategyTests.Item>))]
internal partial class PriorityQueueStrategyTestCrdtAotContext : CrdtAotContext
{
}