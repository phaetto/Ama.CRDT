namespace Ama.CRDT.ShowCase.CollaborativeEditing;

using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Services;
using Ama.CRDT.ShowCase.CollaborativeEditing.Services;

public partial class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NetworkBroker _networkBroker;
    private int _editorCount = 0;

    [Obsolete("Designer only", true)]
    public MainForm()
    {
        InitializeComponent();
        _serviceProvider = null!;
        _networkBroker = null!;
    }

    public MainForm(IServiceProvider serviceProvider, NetworkBroker networkBroker)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _networkBroker = networkBroker ?? throw new ArgumentNullException(nameof(networkBroker));
        
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
        _editorCount++;
        string replicaId = $"editor-{_editorCount}";

        // We create an isolated DI scope for each editor to give it a unique replica identity and metadata manager
        var scopeFactory = _serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        var scope = scopeFactory.CreateScope(replicaId);

        var editorForm = new EditorForm(scope, replicaId, _networkBroker);
        editorForm.Show();
    }
}