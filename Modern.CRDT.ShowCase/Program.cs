using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modern.CRDT.Extensions;
using Modern.CRDT.ShowCase;
using Modern.CRDT.ShowCase.Services;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) =>
            {
                services.AddJsonCrdt(options =>
                {
                    // This is a default replicaId, not used by the simulation tasks which get their own unique IDs.
                    options.ReplicaId = "default-replica"; 
                });

                // Register the custom comparer for the User type in arrays.
                services.AddJsonCrdtComparer<UserByIdComparer>();
                
                // Register services for the simulation.
                services.AddSingleton<IInMemoryDatabaseService, InMemoryDatabaseService>();
                services.AddSingleton<SimulationRunner>();
            })
            .Build();

        Console.WriteLine("Starting CRDT Showcase Simulation...");
        
        var runner = host.Services.GetRequiredService<SimulationRunner>();
        await runner.RunAsync();
        
        Console.WriteLine("\nSimulation finished. Press any key to exit.");
        Console.ReadKey();
    }
}