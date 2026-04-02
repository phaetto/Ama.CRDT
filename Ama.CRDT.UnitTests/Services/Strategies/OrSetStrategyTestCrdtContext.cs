namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for OrSetStrategy unit tests to provide AOT-compatible property metadata.
/// </summary>
[CrdtSerializable(typeof(OrSetStrategyTests.TestModel))]
[CrdtSerializable(typeof(List<string>))]
internal partial class OrSetStrategyTestCrdtContext : CrdtContext
{
}