namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for PriorityQueueStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtSerializable(typeof(PriorityQueueStrategyTests.TestModel))]
[CrdtSerializable(typeof(PriorityQueueStrategyTests.Item))]
[CrdtSerializable(typeof(List<PriorityQueueStrategyTests.Item>))]
internal partial class PriorityQueueStrategyTestCrdtContext : CrdtContext
{
}