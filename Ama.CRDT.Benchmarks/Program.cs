using BenchmarkDotNet.Running;

namespace Ama.CRDT.Benchmarks;

/// <summary>
/// Main entry point for the benchmark application.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs all benchmarks defined in this assembly.
    /// </summary>
    /// <param name="args">Command-line arguments passed to BenchmarkDotNet.</param>
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
#if !DEBUG
        Console.ReadLine();
#endif
    }
}