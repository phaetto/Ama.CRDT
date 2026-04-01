namespace Ama.CRDT.ShowCase.CollaborativeEditing;

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.ShowCase.CollaborativeEditing.Models;
using Ama.CRDT.ShowCase.CollaborativeEditing.Services;

public sealed class EditorForm : Form
{
    private readonly IServiceScope _scope;
    private readonly string _replicaId;
    private readonly NetworkBroker _networkBroker;

    private readonly IAsyncCrdtApplicator _applicator;
    private readonly ICrdtPatcher _patcher;

    private readonly CrdtDocument<SharedDocument> _document;
    private readonly TextBox _textBox;
    private readonly Timer _typingTimer;
    
    private bool _isApplyingNetworkPatch = false;

    public EditorForm(IServiceScope scope, string replicaId, NetworkBroker networkBroker)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _replicaId = string.IsNullOrWhiteSpace(replicaId) ? throw new ArgumentException("Replica ID cannot be empty", nameof(replicaId)) : replicaId;
        _networkBroker = networkBroker ?? throw new ArgumentNullException(nameof(networkBroker));

        _applicator = scope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
        _patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        var metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();

        // Initialize our local CRDT state
        var state = new SharedDocument();
        var metadata = metadataManager.Initialize(state);
        _document = new CrdtDocument<SharedDocument>(state, metadata);

        _networkBroker.MessageReceived += NetworkBroker_MessageReceived;
        
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

        SetupUi();
    }

    private void SetupUi()
    {
        Text = $"Editor Replica - {_replicaId}";
        Size = new Size(650, 400);

        _textBox.TextChanged += TextBox_TextChanged;
        Controls.Add(_textBox);

        FormClosed += EditorForm_FormClosed;
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
            // First apply locally so our internal metadata tracking updates correctly
            var result = await _applicator.ApplyPatchAsync(_document, patch);
            
            // If everything is valid, push it to the other replicas
            if (result.UnappliedOperations == null || result.UnappliedOperations.Count == 0)
            {
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

        try
        {
            // Check if user has un-broadcasted changes typed recently. If so, generate and sync them first.
            if (_typingTimer.Enabled)
            {
                _typingTimer.Stop();
                await GenerateAndBroadcastPatchAsync();
            }

            // Apply incoming patch to our underlying document
            await _applicator.ApplyPatchAsync(_document, e.Patch);
            
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
        _typingTimer.Dispose();
        _scope.Dispose(); // Cleans up replica-scoped services
    }
}