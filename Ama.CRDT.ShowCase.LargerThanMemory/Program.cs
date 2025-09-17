using Ama.CRDT.Extensions;
using Ama.CRDT.ShowCase.LargerThanMemory;
using Ama.CRDT.ShowCase.LargerThanMemory.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    services.AddCrdt();
    services.AddCrdtPartitionStreamProvider<FileSystemPartitionStreamProvider>();

    services.AddScoped<DataGeneratorService>();
    services.AddScoped<UiService>();

    services.AddSingleton<SimulationRunner>();
});

var app = builder.Build();

var simulation = app.Services.GetRequiredService<SimulationRunner>();

await simulation.RunAsync();