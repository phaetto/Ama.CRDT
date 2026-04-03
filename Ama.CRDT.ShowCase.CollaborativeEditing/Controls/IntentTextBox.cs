namespace Ama.CRDT.ShowCase.CollaborativeEditing.Controls;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.ShowCase.CollaborativeEditing.Models;

/// <summary>
/// A custom TextBox control that behaves exactly like a standard multiline text editor,
/// but internally tracks line changes and explicitly generates CRDT Intents (Insert, SetIndex, Remove).
/// By moving the GenerateOperationAsync calls here, the Roslyn analyzers can statically 
/// verify that the intents match the Strategy on the target property.
/// </summary>
public sealed class IntentTextBox : TextBox
{
    private string[] cachedLines = Array.Empty<string>();
    private bool isApplyingExternalChange;
    private bool isProcessing;
    private bool pendingChanges;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAsyncCrdtPatcher? Patcher { get; set; }
    
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<CrdtDocument<SharedDocument>>? DocumentProvider { get; set; }
    
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<CrdtPatch, Task>? OnPatchGenerated { get; set; }

    public IntentTextBox()
    {
        Multiline = true;
        Dock = DockStyle.Fill;
        ScrollBars = ScrollBars.Vertical;
        Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point);
    }

    public void ApplyExternalLines(IEnumerable<string> externalLines, int cursorSelectionStart)
    {
        isApplyingExternalChange = true;
        
        Lines = Enumerable.ToArray(externalLines);
        cachedLines = Lines;
        
        if (cursorSelectionStart <= Text.Length)
        {
            SelectionStart = cursorSelectionStart;
        }
        else
        {
            SelectionStart = Text.Length;
        }
        
        isApplyingExternalChange = false;
    }

    protected override async void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);

        var currentPatcher = Patcher;
        var currentDocProvider = DocumentProvider;
        var currentOnPatch = OnPatchGenerated;

        if (isApplyingExternalChange || currentPatcher == null || currentDocProvider == null || currentOnPatch == null) return;

        // Serialize async patch generation so rapid typing doesn't create overlapping states
        if (isProcessing)
        {
            pendingChanges = true;
            return;
        }

        isProcessing = true;
        try
        {
            do
            {
                pendingChanges = false;
                await ProcessChangesAsync(currentPatcher, currentDocProvider, currentOnPatch);
            } while (pendingChanges);
        }
        finally
        {
            isProcessing = false;
        }
    }

    private async Task ProcessChangesAsync(IAsyncCrdtPatcher patcher, Func<CrdtDocument<SharedDocument>> documentProvider, Func<CrdtPatch, Task> onPatchGenerated)
    {
        string[] oldLines = cachedLines;
        string[] newLines = Lines;
        
        cachedLines = newLines;

        // Perform a quick linear diff to find the changed range
        int start = 0;
        while (start < oldLines.Length && start < newLines.Length && oldLines[start] == newLines[start])
            start++;
            
        int oldEnd = oldLines.Length - 1;
        int newEnd = newLines.Length - 1;
        
        while (oldEnd >= start && newEnd >= start && oldLines[oldEnd] == newLines[newEnd])
        {
            oldEnd--;
            newEnd--;
        }

        int oldLength = oldEnd - start + 1;
        int newLength = newEnd - start + 1;

        if (oldLength == 0 && newLength == 0) return;

        int replaceCount = Math.Min(oldLength, newLength);
        
        var document = documentProvider();
        var operations = new List<CrdtOperation>();

        // 1. Replacements (Lines modified)
        // Since RgaStrategy DOES NOT support SetIndexIntent directly, we must translate a replacement 
        // into a Remove followed by an Insert at the same index to satisfy the Roslyn analyzer and the CRDT rules.
        for (int i = 0; i < replaceCount; i++)
        {
            // Remove the old line
            var removeOp = await patcher.GenerateOperationAsync(document, x => x.Lines, new RemoveIntent(start + i));
            operations.Add(removeOp);
            
            // Insert the new line in its place
            var insertOp = await patcher.GenerateOperationAsync(document, x => x.Lines, new InsertIntent(start + i, newLines[start + i]));
            operations.Add(insertOp);
        }

        // 2. Additions or Deletions
        if (newLength > oldLength)
        {
            // Insertions
            for (int i = replaceCount; i < newLength; i++)
            {
                // The Roslyn analyzer will inspect this exact statement
                var op = await patcher.GenerateOperationAsync(document, x => x.Lines, new InsertIntent(start + i, newLines[start + i]));
                operations.Add(op);
            }
        }
        else if (oldLength > newLength)
        {
            // Deletions. We delete from the same index repeatedly because elements shift left.
            for (int i = replaceCount; i < oldLength; i++)
            {
                // The Roslyn analyzer will inspect this exact statement
                var op = await patcher.GenerateOperationAsync(document, x => x.Lines, new RemoveIntent(start + replaceCount));
                operations.Add(op);
            }
        }

        if (operations.Count > 0)
        {
            await onPatchGenerated(new CrdtPatch(operations));
        }
    }
}