namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for MaxWinsStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtAotType(typeof(MaxWinsStrategyTests.TestModel))]
internal partial class MaxWinsStrategyTestCrdtAotContext : CrdtAotContext
{
}