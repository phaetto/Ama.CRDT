namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

[CrdtSerializable(typeof(AverageRegisterStrategyTests.TestModel))]
internal partial class AverageRegisterTestCrdtContext : CrdtContext
{
}

public sealed class AverageRegisterStrategyTests : IDisposable
{
    internal sealed class TestModel { public decimal Rating { get; set; } }

    private readonly IServiceScope scopeA;
    private readonly IServiceScope scopeB;
    private readonly AverageRegisterStrategy strategyA;
    private readonly AverageRegisterStrategy strategyB;
    private readonly ICrdtTimestampProvider timestampProvider;
    private const string Path = "$.rating";

    public AverageRegisterStrategyTests()
    {
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddCrdtAotContext<AverageRegisterTestCrdtContext>();

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();

        scopeA = scopeFactory.CreateScope("A");
        scopeB = scopeFactory.CreateScope("B");

        strategyA = scopeA.ServiceProvider.GetRequiredService<AverageRegisterStrategy>();
        strategyB = scopeB.ServiceProvider.GetRequiredService<AverageRegisterStrategy>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
        scopeB.Dispose();
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateUpsert_WhenValueChanges()
    {
        // Arrange
        var operations = new List<CrdtOperation>();
        var property = new CrdtPropertyInfo(
            nameof(TestModel.Rating),
            "rating",
            typeof(decimal),
            true,
            true,
            obj => ((TestModel)obj).Rating,
            (obj, val) => ((TestModel)obj).Rating = (decimal)val!,
            null,
            Array.Empty<CrdtStrategyDecoratorAttribute>()
        );
        var originalRoot = new TestModel { Rating = 3.5m };
        var modifiedRoot = new TestModel { Rating = 4.0m };
        var context = new GeneratePatchContext(
            operations,
            new List<DifferentiateObjectContext>(),
            Path,
            property,
            3.5m,
            4.0m,
            originalRoot,
            modifiedRoot,
            new CrdtMetadata(),
            timestampProvider.Create(1L),
            0
        );
        
        // Act
        strategyA.GeneratePatch(context);

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Upsert);
        op.Value.ShouldBe(4.0m);
    }

    [Fact]
    public void GenerateOperation_ShouldCreateUpsert_WhenIntentIsSetIntent()
    {
        // Arrange
        var intent = new SetIntent(5.5m);
        var property = new CrdtPropertyInfo(
            nameof(TestModel.Rating),
            "rating",
            typeof(decimal),
            true,
            true,
            obj => ((TestModel)obj).Rating,
            (obj, val) => ((TestModel)obj).Rating = (decimal)val!,
            null,
            Array.Empty<CrdtStrategyDecoratorAttribute>()
        );
        var metadata = new CrdtMetadata();
        var context = new GenerateOperationContext(
            new TestModel(),
            metadata,
            Path,
            property,
            intent,
            timestampProvider.Create(1L),
            0
        );

        // Act
        var operation = strategyA.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Upsert);
        operation.Value.ShouldBe(5.5m);
        operation.JsonPath.ShouldBe(Path);
        operation.ReplicaId.ShouldBe("A");
    }

    [Fact]
    public void GenerateOperation_ShouldThrowNotSupportedException_ForUnsupportedIntent()
    {
        // Arrange
        var intent = new RemoveIntent(0);
        var property = new CrdtPropertyInfo(
            nameof(TestModel.Rating),
            "rating",
            typeof(decimal),
            true,
            true,
            obj => ((TestModel)obj).Rating,
            (obj, val) => ((TestModel)obj).Rating = (decimal)val!,
            null,
            Array.Empty<CrdtStrategyDecoratorAttribute>()
        );
        var metadata = new CrdtMetadata();
        var context = new GenerateOperationContext(
            new TestModel(),
            metadata,
            Path,
            property,
            intent,
            timestampProvider.Create(1L),
            0
        );

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategyA.GenerateOperation(context));
    }

    [Fact]
    public void ApplyOperation_ShouldCalculateCorrectAverage()
    {
        // Arrange
        var model = new TestModel();
        var metadata = new CrdtMetadata();
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 5m, timestampProvider.Create(1L), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", Path, OperationType.Upsert, 10m, timestampProvider.Create(2L), 0);
        
        // Act
        strategyA.ApplyOperation(new ApplyOperationContext(model, metadata, op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model, metadata, op2));
        
        // Assert
        model.Rating.ShouldBe(7.5m); // (5 + 10) / 2
    }
    
    [Fact]
    public void ApplyOperation_ShouldBeIdempotent()
    {
        // Arrange
        var model = new TestModel();
        var metadata = new CrdtMetadata();
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 5m, timestampProvider.Create(1L), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", Path, OperationType.Upsert, 10m, timestampProvider.Create(2L), 0);
        
        // Act
        strategyA.ApplyOperation(new ApplyOperationContext(model, metadata, op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model, metadata, op2));
        strategyA.ApplyOperation(new ApplyOperationContext(model, metadata, op2)); // Apply second op again
        
        // Assert
        model.Rating.ShouldBe(7.5m);
        metadata.AverageRegisters[Path].Count.ShouldBe(2);
    }
    
    [Fact]
    public void ApplyOperation_ShouldBeCommutative()
    {
        // Arrange
        var model1 = new TestModel();
        var metadata1 = new CrdtMetadata();
        var model2 = new TestModel();
        var metadata2 = new CrdtMetadata();

        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 5m, timestampProvider.Create(1L), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", Path, OperationType.Upsert, 10m, timestampProvider.Create(2L), 0);
        
        // Act: Apply in different orders
        strategyA.ApplyOperation(new ApplyOperationContext(model1, metadata1, op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model1, metadata1, op2));
        
        strategyA.ApplyOperation(new ApplyOperationContext(model2, metadata2, op2));
        strategyA.ApplyOperation(new ApplyOperationContext(model2, metadata2, op1));

        // Assert
        model1.Rating.ShouldBe(7.5m);
        model2.Rating.ShouldBe(7.5m);
        model1.Rating.ShouldBe(model2.Rating);
    }
    
    [Fact]
    public void ApplyOperation_ShouldBeAssociative()
    {
        // Arrange
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 5m, timestampProvider.Create(1L), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r2", Path, OperationType.Upsert, 10m, timestampProvider.Create(2L), 0);
        var op3 = new CrdtOperation(Guid.NewGuid(), "r3", Path, OperationType.Upsert, 15m, timestampProvider.Create(3L), 0);

        var operations = new[] { op1, op2, op3 };
        var permutations = GetPermutations(operations, 3);
        var finalStates = new List<decimal>();

        // Act
        foreach(var p in permutations)
        {
            var model = new TestModel();
            var meta = new CrdtMetadata();
            foreach(var op in p)
            {
                strategyA.ApplyOperation(new ApplyOperationContext(model, meta, op));
            }
            finalStates.Add(model.Rating);
        }

        // Assert
        var expectedAverage = (5m + 10m + 15m) / 3;
        finalStates.ShouldAllBe(s => s == expectedAverage);
        finalStates.Count.ShouldBe(6);
    }

    [Fact]
    public void ApplyOperation_ShouldUseLastWriteWins_ForSameReplica()
    {
        // Arrange
        var model = new TestModel();
        var metadata = new CrdtMetadata();
        var op1 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 5m, timestampProvider.Create(1L), 0);
        var op2 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 8m, timestampProvider.Create(2L), 0); // Newer timestamp
        var op3 = new CrdtOperation(Guid.NewGuid(), "r1", Path, OperationType.Upsert, 3m, timestampProvider.Create(1L), 0); // Older timestamp
        
        // Act
        strategyA.ApplyOperation(new ApplyOperationContext(model, metadata, op1));
        strategyA.ApplyOperation(new ApplyOperationContext(model, metadata, op2));
        strategyA.ApplyOperation(new ApplyOperationContext(model, metadata, op3)); // This one should be ignored
        
        // Assert
        model.Rating.ShouldBe(8m); // Only one value from r1
        metadata.AverageRegisters[Path].Count.ShouldBe(1);
        metadata.AverageRegisters[Path]["r1"].Value.ShouldBe(8m);
    }

    [Fact]
    public void Compact_ShouldNotModifyMetadata_AsStrategyDoesNotMaintainTombstones()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var timestamp = timestampProvider.Create(1L);
        metadata.AverageRegisters[Path] = new Dictionary<string, AverageRegisterValue>
        {
            { "r1", new AverageRegisterValue(5m, timestamp) }
        };

        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>())).Returns(true);

        var context = new CompactionContext(metadata, mockPolicy.Object, "Rating", Path, new TestModel());

        // Act
        strategyA.Compact(context);

        // Assert
        metadata.AverageRegisters[Path].ShouldContainKey("r1");
        metadata.AverageRegisters[Path]["r1"].Value.ShouldBe(5m);
        mockPolicy.Verify(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>()), Times.Never);
    }
    
    private IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });

        var enumerable = list as T[] ?? list.ToArray();
        return GetPermutations(enumerable, length - 1)
            .SelectMany(t => enumerable.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }
}