namespace Ama.CRDT.UnitTests.Services.Decorators;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.UnitTests.Services.Partitioning;

/// <summary>
/// A dedicated CrdtAotContext for the decorators unit tests to provide AOT-compatible 
/// property metadata for test-specific models.
/// </summary>
[CrdtAotType(typeof(TestModel))]
[CrdtAotType(typeof(MultiPartitionedModel))]
internal partial class DecoratorsTestCrdtAotContext : CrdtAotContext
{
}