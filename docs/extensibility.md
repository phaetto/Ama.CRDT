# Extensibility & Customization

Ama.CRDT is built to be highly extensible. You can add your own conflict resolution strategies, custom equality comparers, logical timestamp providers, and garbage collection policies.

## Extensibility: Creating Custom Strategies

You can extend the library with your own conflict resolution logic by creating a custom strategy.

### 1. Create a Custom Attribute

Create an attribute inheriting from `CrdtStrategyAttribute`.

```csharp
public sealed class MyCustomStrategyAttribute() : CrdtStrategyAttribute(typeof(MyCustomStrategy));
```

### 2. Implement `ICrdtStrategy`

Create a class that implements `ICrdtStrategy`, using the context objects for parameters.

```csharp
using Ama.CRDT.Services.Strategies;
using Ama.CRDT.Models;

public sealed class MyCustomStrategy : ICrdtStrategy
{
    public void GeneratePatch(GeneratePatchContext context)
    {
        // Add custom diffing logic here
        // var (patcher, operations, path, ...) = context;
    }

    public CrdtOperation GenerateOperation(GenerateOperationContext context)
    {
        // Handle explicit intents (e.g., IncrementIntent, AddIntent) here
        throw new NotSupportedException();
    }

    public CrdtOperationStatus ApplyOperation(ApplyOperationContext context)
    {
        // Add custom application logic here
        // var (root, metadata, operation) = context;
        return CrdtOperationStatus.Applied;
    }

    public void Compact(CompactionContext context)
    {
        // Add logic to prune tombstones managed by your strategy based on the context.Policy
    }
}
```

### 3. Register in the DI Container

Register your new strategy with a scoped lifetime.

```csharp
// In Program.cs
// ...
builder.Services.AddCrdt();

// Register the custom strategy with a scoped lifetime
builder.Services.AddScoped<MyCustomStrategy>();

// Make it available to the strategy provider
builder.Services.AddScoped<ICrdtStrategy>(sp => sp.GetRequiredService<MyCustomStrategy>());
```

You can now use `[MyCustomStrategy]` on your POCO properties.

## Advanced Extensibility

### Customizing Array Element Comparison

By default, collection strategies use deep equality. To identify complex objects by a unique property (like an `Id`), implement `IElementComparer`.

#### 1. Implement `IElementComparer`

**Example:** A comparer for `User` objects that uses the `Id` property.

*Services/UserComparer.cs*
```csharp
using Ama.CRDT.Services.Providers;
using System.Diagnostics.CodeAnalysis;
using YourApp.Models; 

public class UserComparer : IElementComparer
{
    public bool CanCompare(Type type) => type == typeof(User);

    public new bool Equals(object? x, object? y)
    {
        if (x is User userX && y is User userY)
        {
            return userX.Id == userY.Id;
        }
        return object.Equals(x, y);
    }

    public int GetHashCode([DisallowNull] object obj)
    {
        return (obj is User user) ? user.Id.GetHashCode() : obj.GetHashCode();
    }
}
```

#### 2. Register the Comparer

Use the `AddCrdtComparer<TComparer>()` extension method.

```csharp
// In Program.cs
builder.Services.AddCrdt();
builder.Services.AddCrdtComparer<UserComparer>();
```

### Providing a Custom Timestamp

You can replace the default timestamping mechanism by implementing `ICrdtTimestampProvider`.

#### 1. Implement `ICrdtTimestampProvider`

The provider must be thread-safe if used concurrently and should be registered with a scoped lifetime.

```csharp
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Models;
using System.Threading;

public sealed class LogicalClockProvider : ICrdtTimestampProvider
{
    private long counter = 0;
    public bool IsContinuous => true;

    public ICrdtTimestamp Now() => new EpochTimestamp(Interlocked.Increment(ref counter));
    public ICrdtTimestamp Init() => new EpochTimestamp(0);
    public ICrdtTimestamp Create(long value) => new EpochTimestamp(value);

    public IEnumerable<ICrdtTimestamp> IterateBetween(ICrdtTimestamp start, ICrdtTimestamp end)
    {
        if (start is not EpochTimestamp s || end is not EpochTimestamp e) yield break;
        for (var i = s.Value + 1; i < e.Value; i++) yield return new EpochTimestamp(i);
    }
}
```

#### 2. Register the Provider

Use `AddCrdtTimestampProvider<TProvider>()` to replace the default.

```csharp
// In Program.cs
builder.Services.AddCrdt();
builder.Services.AddCrdtTimestampProvider<LogicalClockProvider>();
```

#### 3. Register a Custom Timestamp Type for Native AOT

If you create your own `ICrdtTimestamp` implementation, you **must** register it in the registry so the STJ polymorphism discriminator logic is mapped correctly.

**Example**: A custom `VectorClockTimestamp`.

*Models/VectorClockTimestamp.cs*
```csharp
public readonly record struct VectorClockTimestamp(long Ticks) : ICrdtTimestamp
{
    public int CompareTo(ICrdtTimestamp? other)
    {
        if (other is null) return 1;

        if (other is not VectorClockTimestamp otherClock)
        {
            throw new ArgumentException("Cannot compare VectorClockTimestamp to other timestamp types.");
        }
        
        return Ticks.CompareTo(otherClock.Ticks);
    }
}
```

Because Ama.CRDT is fully AOT friendly, you must also add your struct to your `CrdtAotContext` and `JsonSerializerContext`:

```csharp
[CrdtAotType(typeof(VectorClockTimestamp))]
public partial class MyAotCrdtContext : CrdtAotContext { }

[JsonSerializable(typeof(VectorClockTimestamp))]
public partial class MyAotJsonContext : JsonSerializerContext { }
```

*In Program.cs*
```csharp
builder.Services.AddCrdt()
    .AddCrdtAotContext<MyAotCrdtContext>()
    .AddCrdtJsonTypeInfoResolver(MyAotJsonContext.Default);

builder.Services.AddCrdtTimestampProvider<VectorClockProvider>(); 

// Register the custom timestamp type with a unique discriminator string.
builder.Services.AddCrdtTimestampType<VectorClockTimestamp>("vector-clock");
```

### Custom Garbage Collection Policies

To execute unique compaction rules, you can create a custom `ICompactionPolicy` and an associated `ICompactionPolicyFactory`. The factory ensures a new policy evaluation context is created dynamically right before garbage collection kicks in.

#### 1. Implement Policy & Factory

```csharp
using Ama.CRDT.Services.GarbageCollection;

public class HighWaterMarkCompactionPolicy : ICompactionPolicy
{
    private readonly long minimumSafeVersion;

    public HighWaterMarkCompactionPolicy(long minimumSafeVersion)
    {
        this.minimumSafeVersion = minimumSafeVersion;
    }

    public bool IsSafeToCompact(CompactionCandidate candidate)
    {
        // Safe to discard tombstones older than our high watermark
        return candidate.Version.HasValue && candidate.Version.Value <= minimumSafeVersion;
    }
}

public class HighWaterMarkPolicyFactory : ICompactionPolicyFactory
{
    private readonly IMyCustomVersionStore store;

    public HighWaterMarkPolicyFactory(IMyCustomVersionStore store)
    {
        this.store = store;
    }

    public ICompactionPolicy CreatePolicy()
    {
        // Evaluated precisely at the moment the applicator finishes a patch
        var watermark = store.GetLowestConfirmedVersion();
        return new HighWaterMarkCompactionPolicy(watermark);
    }
}
```

#### 2. Register the Policy Factory

```csharp
// In Program.cs
builder.Services.AddCrdt()
    .AddCrdtCompactionPolicyFactory<HighWaterMarkPolicyFactory>()
    .AddCrdtApplicatorDecorator<CompactingApplicatorDecorator>(DecoratorBehavior.After);
```