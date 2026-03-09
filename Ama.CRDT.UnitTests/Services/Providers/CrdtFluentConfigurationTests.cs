namespace Ama.CRDT.UnitTests.Services.Providers;

using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes.Decorators;
using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.Services.Strategies.Decorators;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public sealed class CrdtFluentConfigurationTests
{
    public class FluentTestModel
    {
        public int DynamicCounter { get; set; }
        
        [CrdtMaxWinsStrategy]
        public int OverriddenProperty { get; set; }

        [CrdtApprovalQuorum(2)]
        public string DecoratedProperty { get; set; } = string.Empty;
    }

    public class FluentComplexDocument
    {
        public string? Title { get; set; }
        public Dictionary<string, int> Metrics { get; set; } = new();
        public List<string> Log { get; set; } = new();
        public FluentNestedConfig? Config { get; set; }
    }

    public class FluentNestedConfig
    {
        public string? SettingA { get; set; }
        public List<string> SubLog { get; set; } = new();
    }

    [Fact]
    public void Builder_ShouldRegisterStrategiesAndDecorators()
    {
        // Arrange
        var builder = new CrdtModelBuilder();
        
        // Act
        builder.Entity<FluentTestModel>()
            .Property(x => x.DynamicCounter).HasStrategy<CounterStrategy>()
            .HasDecorator<EpochBoundStrategy>()
            .HasDecorator<ApprovalQuorumStrategy>();

        var registry = builder.Build();
        var propInfo = typeof(FluentTestModel).GetProperty(nameof(FluentTestModel.DynamicCounter))!;

        // Assert
        registry.TryGetStrategy(propInfo, out var strategyType).ShouldBeTrue();
        strategyType.ShouldBe(typeof(CounterStrategy));

        registry.TryGetDecorators(propInfo, out var decorators).ShouldBeTrue();
        decorators.ShouldNotBeNull();
        decorators.Count.ShouldBe(2);
        decorators[0].ShouldBe(typeof(EpochBoundStrategy));
        decorators[1].ShouldBe(typeof(ApprovalQuorumStrategy));
    }

    [Fact]
    public void Builder_WithInvalidExpression_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new CrdtModelBuilder();
        var entityBuilder = builder.Entity<FluentTestModel>();

        // Act
        Action action = () => entityBuilder.Property(x => x.ToString()); // Not a property access

        // Assert
        action.ShouldThrow<ArgumentException>()
              .Message.ShouldContain("Expression must end in a property access");
    }

    [Fact]
    public void CrdtStrategyProvider_ShouldPreferRegistryOverAttributes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt(options =>
        {
            options.Entity<FluentTestModel>()
                .Property(x => x.OverriddenProperty).HasStrategy<MinWinsStrategy>(); // Overriding MaxWins
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.GetRequiredService<ICrdtScopeFactory>().CreateScope("test");
        var strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();

        var propInfo = typeof(FluentTestModel).GetProperty(nameof(FluentTestModel.OverriddenProperty))!;
        
        // Act
        var strategy = strategyProvider.GetBaseStrategy(propInfo);

        // Assert
        strategy.ShouldBeOfType<MinWinsStrategy>();
    }

    [Fact]
    public void CrdtStrategyProvider_ShouldFallbackToAttributes_WhenNotInRegistry()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt(options =>
        {
            // Registering something else to ensure registry is active but empty for OverriddenProperty
            options.Entity<FluentTestModel>().Property(x => x.DynamicCounter).HasStrategy<CounterStrategy>();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.GetRequiredService<ICrdtScopeFactory>().CreateScope("test");
        var strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();

        var propInfo = typeof(FluentTestModel).GetProperty(nameof(FluentTestModel.OverriddenProperty))!;
        
        // Act
        var strategy = strategyProvider.GetBaseStrategy(propInfo);

        // Assert
        strategy.ShouldBeOfType<MaxWinsStrategy>();
    }

    [Fact]
    public void CrdtStrategyProvider_ShouldCompletelyOverrideDecorators()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt(options =>
        {
            // We override the decorators completely for this property via the Fluent API
            options.Entity<FluentTestModel>()
                .Property(x => x.DecoratedProperty)
                .HasDecorator<EpochBoundStrategy>(); // Original attribute is ApprovalQuorum
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.GetRequiredService<ICrdtScopeFactory>().CreateScope("test");
        var strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();

        var propInfo = typeof(FluentTestModel).GetProperty(nameof(FluentTestModel.DecoratedProperty))!;
        
        // Act
        var topStrategy = strategyProvider.GetStrategy(propInfo);

        // Assert
        // Should get the EpochBoundStrategy decorator, ignoring the ApprovalQuorum attribute
        topStrategy.ShouldBeOfType<EpochBoundStrategy>();
    }
    
    [Fact]
    public void CrdtStrategyProvider_ShouldUseDefaultStrategy_WhenNeitherRegistryNorAttributesExist()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt(); // No fluent config

        using var provider = services.BuildServiceProvider();
        using var scope = provider.GetRequiredService<ICrdtScopeFactory>().CreateScope("test");
        var strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();

        var propInfo = typeof(FluentTestModel).GetProperty(nameof(FluentTestModel.DynamicCounter))!;
        
        // Act
        var strategy = strategyProvider.GetBaseStrategy(propInfo);

        // Assert
        strategy.ShouldBeOfType<LwwStrategy>(); // Default fallback for primitive properties
    }

    [Fact]
    public void CrdtStrategyProvider_ShouldResolveComposedAndDecoratedStrategies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt(options =>
        {
            options.Entity<FluentTestModel>()
                .Property(x => x.DecoratedProperty)
                .HasStrategy<LwwStrategy>() // Explicit base strategy
                .HasDecorator<EpochBoundStrategy>() // First decorator
                .HasDecorator<ApprovalQuorumStrategy>(); // Second decorator
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.GetRequiredService<ICrdtScopeFactory>().CreateScope("test");
        var strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();

        var propInfo = typeof(FluentTestModel).GetProperty(nameof(FluentTestModel.DecoratedProperty))!;
        
        // Act
        var topStrategy = strategyProvider.GetStrategy(propInfo);
        var baseStrategy = strategyProvider.GetBaseStrategy(propInfo);

        // Assert
        topStrategy.ShouldNotBeNull();
        topStrategy.ShouldNotBeOfType<LwwStrategy>(); // It should be decorated
        
        // The base strategy should be perfectly resolved to the configured innermost strategy
        baseStrategy.ShouldBeOfType<LwwStrategy>();
        
        // The outermost strategy is expected to be the last decorator added in the chain
        topStrategy.ShouldBeOfType<ApprovalQuorumStrategy>();
    }

    [Fact]
    public void Builder_ShouldConfigureStrategiesForComposablePocoWithoutAttributes()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // We configure multiple separate POCOs that form a composable document
        // totally bypassing the need for property attributes.
        services.AddCrdt(options =>
        {
            options.Entity<FluentComplexDocument>()
                .Property(x => x.Metrics).HasStrategy<MinWinsMapStrategy>()
                .Property(x => x.Log).HasStrategy<LseqStrategy>();

            options.Entity<FluentNestedConfig>()
                .Property(x => x.SettingA).HasStrategy<LwwStrategy>()
                .HasDecorator<EpochBoundStrategy>()
                .Property(x => x.SubLog).HasStrategy<ArrayLcsStrategy>();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.GetRequiredService<ICrdtScopeFactory>().CreateScope("test");
        var strategyProvider = scope.ServiceProvider.GetRequiredService<ICrdtStrategyProvider>();

        // Act & Assert - Validate Root Object configuration
        var metricsProp = typeof(FluentComplexDocument).GetProperty(nameof(FluentComplexDocument.Metrics))!;
        strategyProvider.GetBaseStrategy(metricsProp).ShouldBeOfType<MinWinsMapStrategy>();

        var logProp = typeof(FluentComplexDocument).GetProperty(nameof(FluentComplexDocument.Log))!;
        strategyProvider.GetBaseStrategy(logProp).ShouldBeOfType<LseqStrategy>();

        // Act & Assert - Validate Nested Object configuration 
        var settingAProp = typeof(FluentNestedConfig).GetProperty(nameof(FluentNestedConfig.SettingA))!;
        strategyProvider.GetBaseStrategy(settingAProp).ShouldBeOfType<LwwStrategy>();
        strategyProvider.GetStrategy(settingAProp).ShouldBeOfType<EpochBoundStrategy>();

        var subLogProp = typeof(FluentNestedConfig).GetProperty(nameof(FluentNestedConfig.SubLog))!;
        strategyProvider.GetBaseStrategy(subLogProp).ShouldBeOfType<ArrayLcsStrategy>();
    }
}