namespace Ama.CRDT.ShowCase.CollaborativeEditing;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Journaling;
using Ama.CRDT.Services.Versioning;
using Ama.CRDT.ShowCase.CollaborativeEditing.Models;
using Ama.CRDT.ShowCase.CollaborativeEditing.Services;
using Ama.CRDT.ShowCase.CollaborativeEditing.Controls;

public sealed class EditorForm : Form
{
    private readonly IServiceScope scope;
    private readonly string replicaId;
    private readonly NetworkBroker networkBroker;

    private readonly IAsyncCrdtApplicator applicator;
    private readonly IAsyncCrdtPatcher patcher;

    private CrdtDocument<SharedDocument> document = default!;
    
    // UI components
    private readonly IntentTextBox textBox;
    private readonly Panel topPanel;
    
    // Monkey mode fields
    private readonly Timer monkeyTimer;
    private readonly CheckBox monkeyModeCheckBox;
    private readonly NumericUpDown monkeyIntervalNumericUpDown;
    private readonly Label intervalLabel;
    private readonly Random rng = new();
    private readonly string[] loremWords = new[] 
    { 
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", 
        "adipiscing", "elit", "sed", "do", "eiusmod", "tempor", 
        "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua" 
    };
    
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
        patcher = scope.ServiceProvider.GetRequiredService<IAsyncCrdtPatcher>();
        
        textBox = new IntentTextBox();
        
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

        Controls.Add(textBox);
        Controls.Add(topPanel);
        
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

        // Simulating global state locally using the document's local clock for the showcase
        var localDvv = scope.ServiceProvider.GetRequiredService<ReplicaContext>().GlobalVersionVector;
        
        // Calculate requirements directly using the simplified overload
        var requirement = syncService.CalculateRequirement(replicaId, localDvv, "Cluster", clusterState);
        
        if (requirement.IsBehind)
        {
            var journalManager = scope.ServiceProvider.GetRequiredService<IJournalManager>();
            var missingOpsStream = journalManager.GetMissingOperationsAsync(requirement);
            
            // Streamline: Directly apply the async stream of missing operations!
            var result = await applicator.ApplyOperationsAsync(document, missingOpsStream);
            document = result.Document;
        }

        networkBroker.RegisterReplica(replicaId, scope.ServiceProvider.GetRequiredService<ReplicaContext>().GlobalVersionVector, () => 
        {
            return JsonSerializer.Serialize(document, CrdtJsonContext.DefaultOptions);
        });

        // Initialize IntentTextBox dependencies and callbacks
        textBox.Patcher = patcher;
        textBox.DocumentProvider = () => document;
        textBox.OnPatchGenerated = async (patch) => await ApplyAndBroadcastPatchAsync(patch, updateUi: false);

        textBox.ApplyExternalLines(document.Data!.Lines, 0);
        isLoaded = true;

        while (backlog.TryDequeue(out var eMsg))
        {
            await ProcessNetworkMessageAsync(eMsg);
        }
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
        if (!isLoaded) return;

        var lines = textBox.Lines.ToList();
        int action = lines.Count == 0 ? 0 : rng.Next(4);

        try
        {
            if (action == 0) // Add line
            {
                lines.Insert(rng.Next(lines.Count + 1), GetRandomSentence());
            }
            else if (action == 1) // Append to line
            {
                int idx = rng.Next(lines.Count);
                lines[idx] = lines[idx] + " " + loremWords[rng.Next(loremWords.Length)];
            }
            else if (action == 2) // Remove line
            {
                if (lines.Count > 1)
                {
                    lines.RemoveAt(rng.Next(lines.Count));
                }
            }
            else if (action == 3) // Change line completely
            {
                int idx = rng.Next(lines.Count);
                lines[idx] = GetRandomSentence();
            }

            // Directly setting lines triggers OnTextChanged in the IntentTextBox, 
            // which calculates the CRDT operations and signals OnPatchGenerated for us!
            textBox.Lines = lines.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private string GetRandomSentence()
    {
        int wordCount = rng.Next(3, 8);
        return string.Join(" ", Enumerable.Range(0, wordCount).Select(_ => loremWords[rng.Next(loremWords.Length)]));
    }

    private async Task ApplyAndBroadcastPatchAsync(CrdtPatch patch, bool updateUi)
    {
        if (patch.Operations.Count > 0)
        {
            var result = await applicator.ApplyPatchAsync(document, patch);
            document = result.Document; // Streamline: result.Document is now the full CrdtDocument struct
            
            if (result.UnappliedOperations == null || result.UnappliedOperations.Count == 0)
            {
                networkBroker.UpdateReplicaState(replicaId, scope.ServiceProvider.GetRequiredService<ReplicaContext>().GlobalVersionVector);
                networkBroker.Broadcast(replicaId, patch);
                
                if (updateUi)
                {
                    textBox.ApplyExternalLines(document.Data!.Lines, textBox.SelectionStart);
                }
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
            var result = await applicator.ApplyPatchAsync(document, e.Patch);
            document = result.Document; // Streamline: result.Document is now the full CrdtDocument struct
            
            networkBroker.UpdateReplicaState(replicaId, scope.ServiceProvider.GetRequiredService<ReplicaContext>().GlobalVersionVector);
            
            textBox.ApplyExternalLines(document.Data!.Lines, textBox.SelectionStart);
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
        monkeyTimer.Dispose();
        scope.Dispose(); 
    }
}