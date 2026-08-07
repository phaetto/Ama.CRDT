namespace Ama.CRDT.UnitTests.Services;

using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Shouldly;
using System;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Extensions;
using Ama.CRDT.Services.Providers;

public sealed class CrdtMergerTests : IDisposable
{
    private readonly ICrdtMerger merger;
    private readonly IServiceScope scope;
    private readonly string testReplicaId;

    public CrdtMergerTests()
    {
        testReplicaId = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddCrdtAotContext<ServicesTestCrdtAotContext>();

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        scope = scopeFactory.CreateScope(testReplicaId);

        merger = scope.ServiceProvider.GetRequiredService<ICrdtMerger>();
    }

    public void Dispose()
    {
        scope.Dispose();
    }

    [Fact]
    public void MergeState_WithNullData_ShouldThrowArgumentNullException()
    {
        // Arrange
        var document1 = new CrdtDocument<CrdtApplicatorTests.TestModel>(null!, new CrdtMetadata());
        var document2 = new CrdtDocument<CrdtApplicatorTests.TestModel>(new CrdtApplicatorTests.TestModel(), new CrdtMetadata());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => merger.MergeState(document1, document2));
        Should.Throw<ArgumentNullException>(() => merger.MergeState(document2, document1));
    }
    
    [Fact]
    public void MergeState_WithNullMetadata_ShouldThrowArgumentNullException()
    {
        // Arrange
        var document1 = new CrdtDocument<CrdtApplicatorTests.TestModel>(new CrdtApplicatorTests.TestModel(), null!);
        var document2 = new CrdtDocument<CrdtApplicatorTests.TestModel>(new CrdtApplicatorTests.TestModel(), new CrdtMetadata());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => merger.MergeState(document1, document2));
        Should.Throw<ArgumentNullException>(() => merger.MergeState(document2, document1));
    }

    [Fact]
    public void MergeState_ShouldMergeCausalHistory()
    {
        // Arrange
        var timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();

        var primaryMeta = new CrdtMetadata();
        primaryMeta.VersionVector["A"] = 5;
        primaryMeta.VersionVector["B"] = 2;
        var op1 = new CrdtOperation(Guid.NewGuid(), "A", "$.path", OperationType.Upsert, "test", timestampProvider.Create(100), 1);
        primaryMeta.SeenExceptions.Add(op1);

        var primaryDoc = new CrdtDocument<CrdtApplicatorTests.TestModel>(new CrdtApplicatorTests.TestModel(), primaryMeta);

        var secondaryMeta = new CrdtMetadata();
        secondaryMeta.VersionVector["B"] = 4;
        secondaryMeta.VersionVector["C"] = 1;
        var op2 = new CrdtOperation(Guid.NewGuid(), "B", "$.path", OperationType.Upsert, "test2", timestampProvider.Create(101), 1);
        secondaryMeta.SeenExceptions.Add(op2);

        var secondaryDoc = new CrdtDocument<CrdtApplicatorTests.TestModel>(new CrdtApplicatorTests.TestModel(), secondaryMeta);

        // Act
        merger.MergeState(primaryDoc, secondaryDoc);

        // Assert
        primaryMeta.VersionVector.Count.ShouldBe(3);
        primaryMeta.VersionVector["A"].ShouldBe(5);
        primaryMeta.VersionVector["B"].ShouldBe(4);
        primaryMeta.VersionVector["C"].ShouldBe(1);

        primaryMeta.SeenExceptions.Count.ShouldBe(2);
        primaryMeta.SeenExceptions.ShouldContain(op1);
        primaryMeta.SeenExceptions.ShouldContain(op2);
    }
    
    [Fact]
    public void MergeState_ShouldDelegateToStrategies_UsingApplicatorToSetupState()
    {
        // Arrange
        var timestampProvider = scope.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
        var applicator = scope.ServiceProvider.GetRequiredService<ICrdtApplicator>();

        var primaryDoc = new CrdtDocument<CrdtApplicatorTests.TestModel>(new CrdtApplicatorTests.TestModel(), new CrdtMetadata());
        var secondaryDoc = new CrdtDocument<CrdtApplicatorTests.TestModel>(new CrdtApplicatorTests.TestModel(), new CrdtMetadata());

        var patch1 = new CrdtPatch(new[] { new CrdtOperation(Guid.NewGuid(), "A", "$.likes", OperationType.Increment, 5m, timestampProvider.Create(1), 1) });
        var patch2 = new CrdtPatch(new[] { new CrdtOperation(Guid.NewGuid(), "B", "$.likes", OperationType.Increment, 10m, timestampProvider.Create(2), 1) });

        applicator.ApplyPatch(primaryDoc, patch1);
        applicator.ApplyPatch(secondaryDoc, patch2);

        // Act
        merger.MergeState(primaryDoc, secondaryDoc);

        // Assert
        primaryDoc.Data!.Likes.ShouldBe(15);
        primaryDoc.Metadata.VersionVector.Count.ShouldBe(2);
        primaryDoc.Metadata.VersionVector["A"].ShouldBe(1);
        primaryDoc.Metadata.VersionVector["B"].ShouldBe(1);
    }
}