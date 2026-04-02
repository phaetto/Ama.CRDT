namespace Ama.CRDT.UnitTests.Services.Strategies;

using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for the ReplicatedTreeStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtSerializable(typeof(ReplicatedTreeStrategyTests.TestModel))]
[CrdtSerializable(typeof(Dictionary<object, TreeNode>))]
[CrdtSerializable(typeof(Guid))]
[CrdtSerializable(typeof(string))]
internal partial class ReplicatedTreeStrategyTestCrdtContext : CrdtContext
{
}