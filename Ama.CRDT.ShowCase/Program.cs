using Ama.CRDT.Extensions;
using Ama.CRDT.ShowCase;
using Ama.CRDT.ShowCase.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) =>
            {
                services.AddCrdt();

                // Register the custom comparer for the string type in arrays.
                services.AddCrdtComparer<CaseInsensitiveStringComparer>();
                
                // Register services for the simulation.
                services.AddSingleton<IInMemoryDatabaseService, InMemoryDatabaseService>();
                services.AddSingleton<SimulationRunner>();
            })
            .Build();

        Console.WriteLine("Starting CRDT Showcase Simulation...");
        
        var runner = host.Services.GetRequiredService<SimulationRunner>();
        await runner.RunAsync();
    }
}