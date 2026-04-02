namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for MinWinsStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtSerializable(typeof(MinWinsStrategyTests.TestModel))]
internal partial class MinWinsStrategyTestCrdtContext : CrdtContext
{
}