namespace Ama.CRDT.UnitTests.Services;

using System.Collections.Generic;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models.Aot;

/// <summary>
/// A dedicated CrdtAotContext for the Services unit tests to provide AOT-compatible 
/// property metadata for test-specific models and collections.
/// </summary>
[CrdtAotType(typeof(CrdtApplicatorTests.TestModel))]
[CrdtAotType(typeof(CrdtComposableArchitectureTests.TestRoot))]
[CrdtAotType(typeof(CrdtComposableArchitectureTests.TestLevel1))]
[CrdtAotType(typeof(CrdtComposableArchitectureTests.TestLevel2))]
[CrdtAotType(typeof(CrdtComposableArchitectureTests.TestTag))]
[CrdtAotType(typeof(CrdtComposableArchitectureTests.ComplexDocument))]
[CrdtAotType(typeof(CrdtComposableArchitectureTests.NestedConfig))]
[CrdtAotType(typeof(CrdtComposableArchitectureTests.DecoratedDocument))]
[CrdtAotType(typeof(CrdtComposableArchitectureTests.ComplexCollectionDocument))]
[CrdtAotType(typeof(CrdtComposableArchitectureTests.ComplexItem))]
[CrdtAotType(typeof(CrdtPatcherTests.TestModel))]
[CrdtAotType(typeof(CrdtPatcherTests.NestedModel))]
[CrdtAotType(typeof(CrdtMetadataManagerTests.OuterDoc))]
[CrdtAotType(typeof(CrdtMetadataManagerTests.InnerDoc))]
[CrdtAotType(typeof(List<CrdtComposableArchitectureTests.TestTag>))]
[CrdtAotType(typeof(List<string>))]
[CrdtAotType(typeof(List<CrdtComposableArchitectureTests.ComplexItem>))]
[CrdtAotType(typeof(List<CrdtMetadataManagerTests.InnerDoc>))]
[CrdtAotType(typeof(Dictionary<string, int>))]
[CrdtAotType(typeof(Dictionary<string, CrdtComposableArchitectureTests.ComplexItem>))]
[CrdtAotType(typeof(Dictionary<string, CrdtMetadataManagerTests.InnerDoc>))]
[CrdtAotType(typeof(object))]
internal partial class ServicesTestCrdtAotContext : CrdtAotContext
{
}