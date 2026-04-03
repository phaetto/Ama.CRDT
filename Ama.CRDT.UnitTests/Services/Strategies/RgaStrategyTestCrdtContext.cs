namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for the RgaStrategy unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtAotType(typeof(RgaStrategyTests.RgaTestModel))]
[CrdtAotType(typeof(List<string>))]
internal partial class RgaStrategyTestCrdtAotContext : CrdtAotContext
{
}