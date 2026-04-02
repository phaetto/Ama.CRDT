namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for the MaxWinsMapStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtSerializable(typeof(MaxWinsMapStrategyTests.TestModel))]
[CrdtSerializable(typeof(Dictionary<string, int>))]
internal partial class MaxWinsMapStrategyTestCrdtContext : CrdtContext
{
}