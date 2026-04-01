namespace Ama.CRDT.ShowCase.CollaborativeEditing;

using System;
using System.Collections.Generic;
using System.Drawing;
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
    private readonly IServiceScope scope;
    private readonly string replicaId;
    private readonly NetworkBroker networkBroker;

    private readonly IAsyncCrdtApplicator applicator;
    private readonly ICrdtPatcher patcher;

    private CrdtDocument<SharedDocument> document = default!;
    private readonly TextBox textBox;
    private readonly Timer typingTimer;
    
    // Monkey mode fields
    private readonly Timer monkeyTimer;
    private readonly CheckBox monkeyModeCheckBox;
    private readonly NumericUpDown monkeyIntervalNumericUpDown;
    private readonly Label intervalLabel;
    private readonly Panel topPanel;
    private readonly Random rng = new();
    private readonly string[] loremWords = new[] 
    { 
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", 
        "adipiscing", "elit", "sed", "do", "eiusmod", "tempor", 
        "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua" 
    };
    
    private bool isApplyingNetworkPatch = false;
    private bool isLoaded = false;
    private readonly Queue<NetworkMessage> backlog = new();

    public EditorForm(IServiceScope scope, string replicaId, NetworkBroker networkBroker)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (string.IsNullOrWhiteSpace(replicaId)) throw new ArgumentException("Replica ID cannot be empty", nameof(replicaId));
        if (networkBroker == null) throw new ArgumentNullException(nameof(networkBroker));

        this.scope = scope;
        this.replicaId = replicaId;
        this.networkBroker = networkBroker;

        applicator = scope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
        patcher = scope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
        
        textBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point)
        };
        
        typingTimer = new Timer { Interval = 500 };
        typingTimer.Tick += TypingTimer_Tick;

        monkeyTimer = new Timer { Interval = 1000 };
        monkeyTimer.Tick += MonkeyTimer_Tick;
        
        topPanel = new Panel { Dock = DockStyle.Top, Height = 40 };

        monkeyModeCheckBox = new CheckBox 
        { 
            Text = "Monkey Mode", 
            Location = new Point(10, 10), 
            AutoSize = true 
        };
        
        intervalLabel = new Label 
        { 
            Text = "Interval (ms):", 
            Location = new Point(120, 12), 
            AutoSize = true 
        };

        monkeyIntervalNumericUpDown = new NumericUpDown 
        { 
            Location = new Point(200, 10), 
            Minimum = 100, 
            Maximum = 10000, 
            Value = 1000,
            Increment = 100
        };

        monkeyModeCheckBox.CheckedChanged += MonkeyModeCheckBox_CheckedChanged;
        monkeyIntervalNumericUpDown.ValueChanged += MonkeyIntervalNumericUpDown_ValueChanged;

        this.networkBroker.MessageReceived += NetworkBroker_MessageReceived;

        SetupUi();
        this.Load += EditorForm_Load;
    }

    private void SetupUi()
    {
        Text = $"Editor Replica - {replicaId}";
        Size = new Size(1000, 800);

        topPanel.Controls.Add(monkeyModeCheckBox);
        topPanel.Controls.Add(intervalLabel);
        topPanel.Controls.Add(monkeyIntervalNumericUpDown);

        Controls.Add(topPanel);
        Controls.Add(textBox);
        textBox.BringToFront();

        FormClosed += EditorForm_FormClosed;
    }

    private async void EditorForm_Load(object? sender, EventArgs e)
    {
        var metadataManager = scope.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        
        var snapshotJson = networkBroker.GetSnapshotJson();
        if (snapshotJson != null)
        {
            document = JsonSerializer.Deserialize<CrdtDocument<SharedDocument>>(snapshotJson, CrdtJsonContext.DefaultOptions)!;
        }
        else
        {
            var state = new SharedDocument();
            var metadata = metadataManager.Initialize(state);
            document = new CrdtDocument<SharedDocument>(state, metadata);
        }

        var clusterState = networkBroker.GetClusterState();
        var syncService = scope.ServiceProvider.GetRequiredService<IVersionVectorSyncService>();
        
        var localDvv = new DottedVersionVector(document.Metadata.VersionVector, new Dictionary<string, ISet<long>>());
        var localContext = new ReplicaContext { ReplicaId = replicaId, GlobalVersionVector = localDvv };
        var targetContext = new ReplicaContext { ReplicaId = "Cluster", GlobalVersionVector = clusterState };
        
        var requirement = syncService.CalculateRequirement(localContext, targetContext);
        if (requirement.IsBehind)
        {
            var journalManager = scope.ServiceProvider.GetRequiredService<IJournalManager>();
            var missingOpsStream = journalManager.GetMissingOperationsAsync(requirement);
            
            var ops = new List<CrdtOperation>();
            await foreach (var jo in missingOpsStream)
            {
                ops.Add(jo.Operation);
            }
            
            if (ops.Count > 0)
            {
                var patch = new CrdtPatch(ops);
                var result = await applicator.ApplyPatchAsync(document, patch);
                document = new CrdtDocument<SharedDocument>(result.Document, document.Metadata);
            }
        }

        networkBroker.RegisterReplica(replicaId, new DottedVersionVector(document.Metadata.VersionVector, new Dictionary<string, ISet<long>>()), () => 
        {
            return JsonSerializer.Serialize(document, CrdtJsonContext.DefaultOptions);
        });

        textBox.Lines = document.Data.Lines.ToArray();
        textBox.TextChanged += TextBox_TextChanged;
        
        isLoaded = true;

        while (backlog.TryDequeue(out var eMsg))
        {
            await ProcessNetworkMessageAsync(eMsg);
        }
    }

    private void TextBox_TextChanged(object? sender, EventArgs e)
    {
        if (isApplyingNetworkPatch) return;
        
        typingTimer.Stop();
        typingTimer.Start();
    }

    private void MonkeyModeCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (monkeyModeCheckBox.Checked)
        {
            monkeyTimer.Start();
        }
        else
        {
            monkeyTimer.Stop();
        }
    }

    private void MonkeyIntervalNumericUpDown_ValueChanged(object? sender, EventArgs e)
    {
        monkeyTimer.Interval = (int)monkeyIntervalNumericUpDown.Value;
    }

    private void MonkeyTimer_Tick(object? sender, EventArgs e)
    {
        if (isApplyingNetworkPatch || !isLoaded) return;

        var lines = textBox.Lines.ToList();
        int action = lines.Count == 0 ? 0 : rng.Next(4);

        switch (action)
        {
            case 0: // Add line
                lines.Insert(rng.Next(lines.Count + 1), GetRandomSentence());
                break;
            case 1: // Append to line
                int idx1 = rng.Next(lines.Count);
                lines[idx1] = lines[idx1] + " " + loremWords[rng.Next(loremWords.Length)];
                break;
            case 2: // Remove line
                if (lines.Count > 1)
                {
                    lines.RemoveAt(rng.Next(lines.Count));
                }
                break;
            case 3: // Change line
                int idx3 = rng.Next(lines.Count);
                lines[idx3] = GetRandomSentence();
                break;
        }

        int selectionStart = textBox.SelectionStart;
        
        // This will trigger TextBox_TextChanged and restart typingTimer to broadcast the patch
        textBox.Lines = lines.ToArray();

        if (selectionStart <= textBox.Text.Length)
        {
            textBox.SelectionStart = selectionStart;
        }
        else 
        {
            textBox.SelectionStart = textBox.Text.Length;
        }
    }

    private string GetRandomSentence()
    {
        int wordCount = rng.Next(3, 8);
        return string.Join(" ", Enumerable.Range(0, wordCount).Select(_ => loremWords[rng.Next(loremWords.Length)]));
    }

    private async void TypingTimer_Tick(object? sender, EventArgs e)
    {
        typingTimer.Stop();
        await GenerateAndBroadcastPatchAsync();
    }

    private async Task GenerateAndBroadcastPatchAsync()
    {
        var currentLines = textBox.Lines.ToList();
        var targetState = new SharedDocument { Lines = currentLines };
        var patch = patcher.GeneratePatch(document, targetState);

        if (patch.Operations.Count > 0)
        {
            var result = await applicator.ApplyPatchAsync(document, patch);
            document = new CrdtDocument<SharedDocument>(result.Document, document.Metadata);
            
            if (result.UnappliedOperations == null || result.UnappliedOperations.Count == 0)
            {
                networkBroker.UpdateReplicaState(replicaId, new DottedVersionVector(document.Metadata.VersionVector, new Dictionary<string, ISet<long>>()));
                networkBroker.Broadcast(replicaId, patch);
            }
        }
    }

    private async void NetworkBroker_MessageReceived(object? sender, NetworkMessage e)
    {
        if (e.SenderId == replicaId) return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => NetworkBroker_MessageReceived(sender, e)));
            return;
        }

        if (!isLoaded)
        {
            backlog.Enqueue(e);
            return;
        }

        await ProcessNetworkMessageAsync(e);
    }

    private async Task ProcessNetworkMessageAsync(NetworkMessage e)
    {
        try
        {
            if (typingTimer.Enabled)
            {
                typingTimer.Stop();
                await GenerateAndBroadcastPatchAsync();
            }

            var result = await applicator.ApplyPatchAsync(document, e.Patch);
            document = new CrdtDocument<SharedDocument>(result.Document, document.Metadata);
            
            networkBroker.UpdateReplicaState(replicaId, new DottedVersionVector(document.Metadata.VersionVector, new Dictionary<string, ISet<long>>()));
            
            isApplyingNetworkPatch = true;
            
            int selectionStart = textBox.SelectionStart;

            textBox.Lines = document.Data.Lines.ToArray();

            if (selectionStart <= textBox.Text.Length)
            {
                textBox.SelectionStart = selectionStart;
            }
            else 
            {
                textBox.SelectionStart = textBox.Text.Length;
            }

            isApplyingNetworkPatch = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error applying patch from {e.SenderId}: {ex.Message}", "CRDT Apply Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EditorForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        networkBroker.MessageReceived -= NetworkBroker_MessageReceived;
        networkBroker.UnregisterReplica(replicaId);
        typingTimer.Dispose();
        monkeyTimer.Dispose();
        scope.Dispose(); 
    }
}