namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for MinWinsStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtAotType(typeof(MinWinsStrategyTests.TestModel))]
internal partial class MinWinsStrategyTestCrdtAotContext : CrdtAotContext
{
}