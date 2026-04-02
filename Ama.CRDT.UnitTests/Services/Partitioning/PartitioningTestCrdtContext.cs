namespace Ama.CRDT.UnitTests.Services.Partitioning;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtContext for the Partitioning unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtSerializable(typeof(MultiPartitionedModel))]
[CrdtSerializable(typeof(PartitionStorageServiceContractTests.TestData))]
[CrdtSerializable(typeof(Dictionary<string, string>))]
internal partial class PartitioningTestCrdtContext : CrdtContext
{
}