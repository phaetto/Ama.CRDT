namespace Ama.CRDT.ShowCase.CollaborativeEditing;

using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Extensions;
using Ama.CRDT.Services.Decorators;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Journaling;
using Ama.CRDT.ShowCase.CollaborativeEditing.Services;

internal static class Program
{
    public static IServiceProvider ServiceProvider { get; private set; } = default!;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();
        
        // Register the core CRDT infrastructure
        services.AddCrdt();

        // Register Memory Journal as Singleton so all replicas share it (simulating a central DB or shared bus)
        services.AddSingleton<MemoryJournal>();
        services.AddSingleton<ICrdtOperationJournal>(sp => sp.GetRequiredService<MemoryJournal>());

        // Decorate pipeline to automatically journal changes and trigger garbage collection routines
        services.AddCrdtApplicatorDecorator<JournalingApplicatorDecorator>();
        services.AddCrdtPatcherDecorator<JournalingPatcherDecorator>();
        services.AddCrdtApplicatorDecorator<CompactingApplicatorDecorator>();

        // Register Global GC policy for CRDT Metadata connected to the network's minimum version state
        services.AddCrdtCompactionPolicyFactory(sp => 
            new GlobalMinimumVersionPolicyFactory(() => sp.GetRequiredService<NetworkBroker>().GetGmvv()));
        
        // Register our showcase-specific services
        services.AddSingleton<NetworkBroker>();
        services.AddTransient<MainForm>();

        ServiceProvider = services.BuildServiceProvider();

        // Resolve the management form through DI
        var mainForm = ServiceProvider.GetRequiredService<MainForm>();
        Application.Run(mainForm);
    }
}