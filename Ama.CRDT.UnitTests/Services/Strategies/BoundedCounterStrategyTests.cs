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

public sealed class BoundedCounterStrategyTests : IDisposable
{
    private sealed class TestModel
    {
        [CrdtBoundedCounterStrategy(0, 100)]
        public int Level { get; set; }
    }

    private readonly IServiceScope scopeA;
    private readonly BoundedCounterStrategy strategy;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly ICrdtTimestampProvider timestampProvider;

    public BoundedCounterStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
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
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Level))!;
        var originalRoot = new TestModel { Level = 50 };
        var modifiedRoot = new TestModel { Level = 60 };
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
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Level))!;
        var metadata = new CrdtMetadata();
        var context = new GenerateOperationContext(
            new TestModel(),
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
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Level))!;
        var metadata = new CrdtMetadata();
        var context = new GenerateOperationContext(
            new TestModel(),
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
        var model = new TestModel { Level = initial };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Level))!;
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
        var model = new NoAttributeModel { Value = 50 };
        var property = typeof(NoAttributeModel).GetProperty(nameof(NoAttributeModel.Value))!;
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
        var model = new TestModel { Level = 50 };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);
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
            var model = new TestModel { Level = 50 };
            var meta = metadataManagerA.Initialize(model);
            var document = new CrdtDocument<TestModel>(model, meta);
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
        metadata.Lww["$.level"] = new CausalTimestamp(timestamp, "A", 1);

        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>())).Returns(true);

        var context = new CompactionContext(metadata, mockPolicy.Object, "Level", "$.level", new TestModel());

        // Act
        strategy.Compact(context);

        // Assert
        metadata.Lww["$.level"].ShouldBe(new CausalTimestamp(timestamp, "A", 1));
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
    
    private sealed class NoAttributeModel { public int Value { get; set; } }
}