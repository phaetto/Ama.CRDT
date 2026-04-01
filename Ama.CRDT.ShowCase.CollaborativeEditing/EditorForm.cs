namespace Ama.CRDT.ShowCase.CollaborativeEditing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Journaling;
using Ama.CRDT.Services.Versioning;
using Ama.CRDT.ShowCase.CollaborativeEditing.Models;
using Ama.CRDT.ShowCase.CollaborativeEditing.Services;

public sealed class EditorForm : Form
{
    private readonly IServiceScope _scope;
    private readonly string _replicaId;
    private readonly NetworkBroker _networkBroker;

    private readonly IAsyncCrdtApplicator _applicator;
    private readonly ICrdtPatcher _patcher;

    private CrdtDocument<SharedDocument> _document = default!;
    private readonly TextBox _textBox;
    private readonly Timer _typingTimer;
    
    private bool _isApplyingNetworkPatch = false;
    private bool _isLoaded = false;
    private readonly Queue<NetworkMessageEventArgs> _backlog = new();

    public EditorForm(IServiceScope scope, string replicaId, NetworkBroker networkBroker)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _replicaId = string.IsNullOrWhiteSpace(replicaId) ? throw new ArgumentException("Replica ID cannot be empty", nameof(replicaId)) : replicaId;
        _networkBroker = networkBroker ?? throw new ArgumentNullException(nameof(networkBroker));

        _applicator = scope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
        _patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        
        _textBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point)
        };
        
        // Timer to batch rapid keystrokes before generating a diff/patch
        _typingTimer = new Timer { Interval = 500 };
        _typingTimer.Tick += TypingTimer_Tick;

        // Catch messages early, they will be backlogged until load is complete
        _networkBroker.MessageReceived += NetworkBroker_MessageReceived;

        SetupUi();
        this.Load += EditorForm_Load;
    }

    private void SetupUi()
    {
        Text = $"Editor Replica - {_replicaId}";
        Size = new Size(650, 400);

        Controls.Add(_textBox);
        FormClosed += EditorForm_FormClosed;
    }

    private async void EditorForm_Load(object? sender, EventArgs e)
    {
        var metadataManager = _scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        
        // 1. Snapshot Init: Pull the existing document from the cluster baseline to avoid replay of ancient events
        var snapshotJson = _networkBroker.GetSnapshotJson();
        if (snapshotJson != null)
        {
            _document = JsonSerializer.Deserialize<CrdtDocument<SharedDocument>>(snapshotJson, CrdtJsonContext.DefaultOptions)!;
        }
        else
        {
            var state = new SharedDocument();
            var metadata = metadataManager.Initialize(state);
            _document = new CrdtDocument<SharedDocument>(state, metadata);
        }

        // 2. Journal Sync: Catch up missing operations via Version Vectors
        var clusterState = _networkBroker.GetClusterState();
        var syncService = _scope.ServiceProvider.GetRequiredService<IVersionVectorSyncService>();
        
        var localDvv = new DottedVersionVector(_document.Metadata.VersionVector, new Dictionary<string, ISet<long>>());
        var localContext = new ReplicaContext { ReplicaId = _replicaId, GlobalVersionVector = localDvv };
        var targetContext = new ReplicaContext { ReplicaId = "Cluster", GlobalVersionVector = clusterState };
        
        var requirement = syncService.CalculateRequirement(localContext, targetContext);
        if (requirement.IsBehind)
        {
            var journalManager = _scope.ServiceProvider.GetRequiredService<IJournalManager>();
            var missingOpsStream = journalManager.GetMissingOperationsAsync(requirement);
            
            var ops = new List<CrdtOperation>();
            await foreach (var jo in missingOpsStream)
            {
                ops.Add(jo.Operation);
            }
            
            if (ops.Count > 0)
            {
                var patch = new CrdtPatch(ops);
                var result = await _applicator.ApplyPatchAsync(_document, patch);
                _document = new CrdtDocument<SharedDocument>(result.Document, _document.Metadata);
            }
        }

        // 3. Register self for active cluster GC tracking
        _networkBroker.RegisterReplica(_replicaId, new DottedVersionVector(_document.Metadata.VersionVector, new Dictionary<string, ISet<long>>()), () => 
        {
            return JsonSerializer.Serialize(_document, CrdtJsonContext.DefaultOptions);
        });

        _textBox.Lines = _document.Data.Lines.ToArray();
        _textBox.TextChanged += TextBox_TextChanged;
        
        _isLoaded = true;

        // Drain any messages that arrived while we were loading
        while (_backlog.TryDequeue(out var eMsg))
        {
            await ProcessNetworkMessageAsync(eMsg);
        }
    }

    private void TextBox_TextChanged(object? sender, EventArgs e)
    {
        // Don't trigger patch generation if the text changed because of a network patch application
        if (_isApplyingNetworkPatch) return;
        
        _typingTimer.Stop();
        _typingTimer.Start();
    }

    private async void TypingTimer_Tick(object? sender, EventArgs e)
    {
        _typingTimer.Stop();
        await GenerateAndBroadcastPatchAsync();
    }

    private async Task GenerateAndBroadcastPatchAsync()
    {
        // Extract lines and compute the difference
        var currentLines = _textBox.Lines.ToList();
        var targetState = new SharedDocument { Lines = currentLines };
        var patch = _patcher.GeneratePatch(_document, targetState);

        if (patch.Operations.Count > 0)
        {
            // Apply locally; the decorator manages pushing it to the Operation Journal
            var result = await _applicator.ApplyPatchAsync(_document, patch);
            _document = new CrdtDocument<SharedDocument>(result.Document, _document.Metadata);
            
            if (result.UnappliedOperations == null || result.UnappliedOperations.Count == 0)
            {
                _networkBroker.UpdateReplicaState(_replicaId, new DottedVersionVector(_document.Metadata.VersionVector, new Dictionary<string, ISet<long>>()));
                _networkBroker.Broadcast(_replicaId, patch);
            }
        }
    }

    private async void NetworkBroker_MessageReceived(object? sender, NetworkMessageEventArgs e)
    {
        if (e.SenderId == _replicaId) return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => NetworkBroker_MessageReceived(sender, e)));
            return;
        }

        if (!_isLoaded)
        {
            _backlog.Enqueue(e);
            return;
        }

        await ProcessNetworkMessageAsync(e);
    }

    private async Task ProcessNetworkMessageAsync(NetworkMessageEventArgs e)
    {
        try
        {
            // Check if user has un-broadcasted changes typed recently. If so, generate and sync them first.
            if (_typingTimer.Enabled)
            {
                _typingTimer.Stop();
                await GenerateAndBroadcastPatchAsync();
            }

            // Apply incoming patch to our underlying document
            var result = await _applicator.ApplyPatchAsync(_document, e.Patch);
            _document = new CrdtDocument<SharedDocument>(result.Document, _document.Metadata);
            
            _networkBroker.UpdateReplicaState(_replicaId, new DottedVersionVector(_document.Metadata.VersionVector, new Dictionary<string, ISet<long>>()));
            
            _isApplyingNetworkPatch = true;
            
            int selectionStart = _textBox.SelectionStart;

            // Re-render text directly from CRDT state
            _textBox.Lines = _document.Data.Lines.ToArray();

            // Best-effort cursor preservation
            if (selectionStart <= _textBox.Text.Length)
            {
                _textBox.SelectionStart = selectionStart;
            }
            else 
            {
                _textBox.SelectionStart = _textBox.Text.Length;
            }

            _isApplyingNetworkPatch = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error applying patch from {e.SenderId}: {ex.Message}", "CRDT Apply Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EditorForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _networkBroker.MessageReceived -= NetworkBroker_MessageReceived;
        _networkBroker.UnregisterReplica(_replicaId);
        _typingTimer.Dispose();
        _scope.Dispose(); // Cleans up replica-scoped services
    }
}