namespace Ama.CRDT.ShowCase.CollaborativeEditing;

using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Extensions;
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
        
        // Register the CRDT infrastructure
        services.AddCrdt();
        
        // Register our showcase-specific services
        services.AddSingleton<NetworkBroker>();
        services.AddTransient<MainForm>();

        ServiceProvider = services.BuildServiceProvider();

        // Resolve the management form through DI
        var mainForm = ServiceProvider.GetRequiredService<MainForm>();
        Application.Run(mainForm);
    }
}