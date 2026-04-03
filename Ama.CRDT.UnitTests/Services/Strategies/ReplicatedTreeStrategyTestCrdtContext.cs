namespace Ama.CRDT.UnitTests.Services.Strategies;

using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for the ReplicatedTreeStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtAotType(typeof(ReplicatedTreeStrategyTests.TestModel))]
[CrdtAotType(typeof(Dictionary<object, TreeNode>))]
[CrdtAotType(typeof(Guid))]
[CrdtAotType(typeof(string))]
internal partial class ReplicatedTreeStrategyTestCrdtAotContext : CrdtAotContext
{
}