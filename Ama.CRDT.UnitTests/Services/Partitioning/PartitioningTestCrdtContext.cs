namespace Ama.CRDT.UnitTests.Services.Partitioning;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for the Partitioning unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtAotType(typeof(MultiPartitionedModel))]
[CrdtAotType(typeof(PartitionStorageServiceContractTests.TestData))]
[CrdtAotType(typeof(Dictionary<string, string>))]
internal partial class PartitioningTestCrdtAotContext : CrdtAotContext
{
}