namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Shouldly;
using Xunit;
using System.Linq;
using Ama.CRDT.Services.Providers;
using System;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Extensions;
using Ama.CRDT.Attributes.Strategies;
using Moq;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Models.Aot;
using Ama.CRDT.Attributes;

[CrdtAotType(typeof(BoundedCounterTestModel))]
[CrdtAotType(typeof(BoundedCounterNoAttributeModel))]
internal partial class BoundedCounterStrategyTestCrdtAotContext : CrdtAotContext
{
}

internal sealed class BoundedCounterTestModel
{
    [CrdtBoundedCounterStrategy(0, 100)]
    public int Level { get; set; }
}

internal sealed class BoundedCounterNoAttributeModel
{
    public int Value { get; set; }
}

public sealed class BoundedCounterStrategyTests : IDisposable
{
    private readonly IServiceScope scopeA;
    private readonly BoundedCounterStrategy strategy;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly ICrdtTimestampProvider timestampProvider;

    public BoundedCounterStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .AddCrdtAotContext<BoundedCounterStrategyTestCrdtAotContext>()
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        scopeA = scopeFactory.CreateScope("A");

        strategy = scopeA.ServiceProvider.GetRequiredService<BoundedCounterStrategy>();
        applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateIncrementOperation_WhenValueChanges()
    {
        // Arrange
        var operations = new List<CrdtOperation>();
        var property = new CrdtPropertyInfo(
            "Level",
            "level",
            typeof(int),
            true,
            true,
            obj => ((BoundedCounterTestModel)obj).Level,
            (obj, val) => ((BoundedCounterTestModel)obj).Level = (int)val!,
            new CrdtBoundedCounterStrategyAttribute(0, 100),
            []);
            
        var originalRoot = new BoundedCounterTestModel { Level = 50 };
        var modifiedRoot = new BoundedCounterTestModel { Level = 60 };
        var context = new GeneratePatchContext(
            operations,
            new List<DifferentiateObjectContext>(),
            "$.level",
            property,
            50,
            60,
            originalRoot,
            modifiedRoot,
            new CrdtMetadata(),
            timestampProvider.Create(1L),
            0
        );
        
        // Act
        strategy.GeneratePatch(context);

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Increment);
        op.Value.ShouldBe(10m);
    }

    [Fact]
    public void GenerateOperation_ShouldCreateIncrement_WhenIntentIsIncrementIntent()
    {
        // Arrange
        var intent = new IncrementIntent(15m);
        var property = new CrdtPropertyInfo(
            "Level",
            "level",
            typeof(int),
            true,
            true,
            obj => ((BoundedCounterTestModel)obj).Level,
            (obj, val) => ((BoundedCounterTestModel)obj).Level = (int)val!,
            new CrdtBoundedCounterStrategyAttribute(0, 100),
            []);
            
        var metadata = new CrdtMetadata();
        var context = new GenerateOperationContext(
            new BoundedCounterTestModel(),
            metadata,
            "$.Level",
            property,
            intent,
            timestampProvider.Create(1L),
            0
        );

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Increment);
        operation.Value.ShouldBe(15m);
        operation.JsonPath.ShouldBe("$.Level");
        operation.ReplicaId.ShouldBe("A");
    }

    [Fact]
    public void GenerateOperation_ShouldThrowNotSupportedException_ForUnsupportedIntent()
    {
        // Arrange
        var intent = new SetIntent(100);
        var property = new CrdtPropertyInfo(
            "Level",
            "level",
            typeof(int),
            true,
            true,
            obj => ((BoundedCounterTestModel)obj).Level,
            (obj, val) => ((BoundedCounterTestModel)obj).Level = (int)val!,
            new CrdtBoundedCounterStrategyAttribute(0, 100),
            []);
            
        var metadata = new CrdtMetadata();
        var context = new GenerateOperationContext(
            new BoundedCounterTestModel(),
            metadata,
            "$.Level",
            property,
            intent,
            timestampProvider.Create(1L),
            0
        );

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategy.GenerateOperation(context));
    }

    [Theory]
    [InlineData(50, 20, 70)]    // Normal increment
    [InlineData(50, -20, 30)]   // Normal decrement
    [InlineData(90, 20, 100)]   // Clamp to max
    [InlineData(10, -20, 0)]    // Clamp to min
    [InlineData(110, -5, 100)]  // Start above max, clamp
    [InlineData(-10, 5, 0)]     // Start below min, clamp
    public void ApplyOperation_ShouldClampValue_WithinBounds(int initial, int delta, int expected)
    {
        // Arrange
        var model = new BoundedCounterTestModel { Level = initial };
        var property = new CrdtPropertyInfo(
            "Level",
            "level",
            typeof(int),
            true,
            true,
            obj => ((BoundedCounterTestModel)obj).Level,
            (obj, val) => ((BoundedCounterTestModel)obj).Level = (int)val!,
            new CrdtBoundedCounterStrategyAttribute(0, 100),
            []);
            
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.level", OperationType.Increment, (decimal)delta, timestampProvider.Create(2L), 1);
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation)
        {
            Target = model,
            Property = property,
            FinalSegment = "level"
        };
        
        // Act
        strategy.ApplyOperation(context);
        
        // Assert
        model.Level.ShouldBe(expected);
    }

    [Fact]
    public void ApplyOperation_ShouldReturnFailure_WhenAttributeIsMissing()
    {
        // Arrange
        var model = new BoundedCounterNoAttributeModel { Value = 50 };
        var property = new CrdtPropertyInfo(
            "Value",
            "value",
            typeof(int),
            true,
            true,
            obj => ((BoundedCounterNoAttributeModel)obj).Value,
            (obj, val) => ((BoundedCounterNoAttributeModel)obj).Value = (int)val!,
            null,
            []);
            
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.value", OperationType.Increment, 10m, timestampProvider.Create(2L), 1);
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation)
        {
            Target = model,
            Property = property,
            FinalSegment = "value"
        };
        
        // Act
        var result = strategy.ApplyOperation(context);

        // Assert
        result.ShouldBe(CrdtOperationStatus.StrategyApplicationFailed);
    }
    
    [Fact]
    public void ApplyPatch_IsIdempotent()
    {
        // Arrange
        var model = new BoundedCounterTestModel { Level = 50 };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<BoundedCounterTestModel>(model, meta);
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new(Guid.NewGuid(), "r1", "$.Level", OperationType.Increment, 10m, timestampProvider.Create(1L), 1)
        });

        // Act
        applicatorA.ApplyPatch(document, patch);
        var scoreAfterFirst = model.Level;
        applicatorA.ApplyPatch(document, patch);

        // Assert
        model.Level.ShouldBe(scoreAfterFirst);
        model.Level.ShouldBe(60);
    }

    [Fact]
    public void ApplyPatch_IsCommutativeAndAssociative()
    {
        // Arrange
        var patch1 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r1", "$.Level", OperationType.Increment, 10m, timestampProvider.Create(1L), 1) });   // 50 -> 60
        var patch2 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r2", "$.Level", OperationType.Increment, -20m, timestampProvider.Create(2L), 1) }); // 60 -> 40
        var patch3 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r3", "$.Level", OperationType.Increment, 70m, timestampProvider.Create(3L), 1) });  // 40 -> 100 (clamped)

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, 3);
        var finalScores = new List<int>();

        // Act
        foreach (var p in permutations)
        {
            var model = new BoundedCounterTestModel { Level = 50 };
            var meta = metadataManagerA.Initialize(model);
            var document = new CrdtDocument<BoundedCounterTestModel>(model, meta);
            foreach (var patch in p)
            {
                applicatorA.ApplyPatch(document, patch);
            }
            finalScores.Add(model.Level);
        }

        // Assert
        // Expected: 50 + 10 - 20 + 70 = 110, which clamps to 100.
        finalScores.ShouldAllBe(s => s == 100);
    }

    [Fact]
    public void Compact_ShouldNotModifyMetadata_AsStrategyDoesNotMaintainTombstones()
    {
        // Arrange
        var metadata = new CrdtMetadata();
        var timestamp = timestampProvider.Create(1L);
        metadata.States["$.level"] = new CausalTimestamp(timestamp, "A", 1);

        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>())).Returns(true);

        var context = new CompactionContext(metadata, mockPolicy.Object, "Level", "$.level", new BoundedCounterTestModel());

        // Act
        strategy.Compact(context);

        // Assert
        metadata.States["$.level"].ShouldBe(new CausalTimestamp(timestamp, "A", 1));
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