namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for the LwwSetStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtSerializable(typeof(LwwSetStrategyTests.TestModel))]
[CrdtSerializable(typeof(List<string>))]
internal partial class LwwSetStrategyTestCrdtContext : CrdtContext
{
}