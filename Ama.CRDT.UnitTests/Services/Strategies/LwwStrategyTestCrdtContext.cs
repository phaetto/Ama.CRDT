namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for the LwwStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtSerializable(typeof(LwwStrategyTests.TestModel))]
[CrdtSerializable(typeof(LwwStrategyTests.NullableTestModel))]
internal partial class LwwStrategyTestCrdtContext : CrdtContext
{
}