using Ama.CRDT.Extensions;
using Ama.CRDT.LargerThanMemory.Streams.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.ShowCase.LargerThanMemory;
using Ama.CRDT.ShowCase.LargerThanMemory.Models;
using Ama.CRDT.ShowCase.LargerThanMemory.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    services.AddCrdt()
        .AddCrdtSerializableType<Comment>("blog-comment")
        .AddCrdtAotContext<LargerThanMemoryCrdtAotContext>()
        .AddCrdtJsonTypeInfoResolver(LargerThanMemoryJsonContext.Default)
        .AddCrdtJournaling<FileSystemOperationJournal>()
        .AddCrdtApplicatorDecorator<JournalingApplicatorDecorator>(DecoratorBehavior.Before)
        .AddCrdtPatcherDecorator<JournalingPatcherDecorator>(DecoratorBehavior.After)
        .AddCrdtApplicatorDecorator<LargerThanMemoryApplicatorDecorator>(DecoratorBehavior.Complex)
        .AddCrdtStreamChunking<FileSystemChunkStreamProvider>()
        .AddCrdtChunkedDocument<BlogPost>()
        .AddCrdtVirtualDocumentProjector<BlogPost, BlogPostSqliteProjector>();

    services.AddScoped<BlogPostReadRepository>();
    services.AddScoped<DataGeneratorService>();
    services.AddScoped<UiService>();
    
    services.AddSingleton<SimulationRunner>();
});

var app = builder.Build();

var simulation = app.Services.GetRequiredService<SimulationRunner>();

await simulation.RunAsync();