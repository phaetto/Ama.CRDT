namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for the LwwStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtAotType(typeof(LwwStrategyTests.TestModel))]
[CrdtAotType(typeof(LwwStrategyTests.NullableTestModel))]
internal partial class LwwStrategyTestCrdtAotContext : CrdtAotContext
{
}