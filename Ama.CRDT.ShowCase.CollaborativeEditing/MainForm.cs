namespace Ama.CRDT.ShowCase.CollaborativeEditing;

using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Services;
using Ama.CRDT.ShowCase.CollaborativeEditing.Services;

public partial class MainForm : Form
{
    private readonly IServiceProvider serviceProvider;
    private readonly NetworkBroker networkBroker;
    private int editorCount = 0;

    [Obsolete("Designer only", true)]
    public MainForm()
    {
        InitializeComponent();
        serviceProvider = null!;
        networkBroker = null!;
    }

    public MainForm(IServiceProvider serviceProvider, NetworkBroker networkBroker)
    {
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));
        if (networkBroker == null) throw new ArgumentNullException(nameof(networkBroker));

        InitializeComponent();
        this.serviceProvider = serviceProvider;
        this.networkBroker = networkBroker;
        
        SetupUi();
    }

    private void SetupUi()
    {
        Text = "CRDT Collaborative Editing Manager";
        Size = new Size(400, 200);
        StartPosition = FormStartPosition.CenterScreen;

        var btnAddEditor = new Button
        {
            Text = "Spawn New Editor Replica",
            Location = new Point(75, 50),
            Size = new Size(250, 40)
        };
        btnAddEditor.Click += BtnAddEditor_Click;

        Controls.Add(btnAddEditor);
    }

    private void BtnAddEditor_Click(object? sender, EventArgs e)
    {
        editorCount++;
        string replicaId = $"editor-{editorCount}";

        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        var scope = scopeFactory.CreateScope(replicaId);

        var editorForm = new EditorForm(scope, replicaId, networkBroker);
        editorForm.Show();
    }
}