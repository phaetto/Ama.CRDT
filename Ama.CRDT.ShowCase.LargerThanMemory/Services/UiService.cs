namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Journaling;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.Services.Versioning;
using Ama.CRDT.ShowCase.LargerThanMemory.Models;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;

public sealed class UiService
{
    private const string CommentsPropertyName = nameof(BlogPost.Comments);
    private const string TagsPropertyName = nameof(BlogPost.Tags);
    private const string DvvStateFilePath = "replica_dvvs.json";

    private readonly IServiceProvider serviceProvider;
    private readonly List<string> replicaIds;
    private readonly List<Guid> blogPostIds;
    private Guid selectedBlogPostId;

    private IServiceScope currentScope;
    private IPartitionManager<BlogPost> partitionManager;
    private string currentReplicaId;

    // Track simulated Global Version Vectors to show causality gaps
    private readonly Dictionary<string, DottedVersionVector> replicaDvvs = new();

    private Window topPane;
    private FrameView rightPane;
    private ListView postListView;
    private TextView postContentView;
    private ListView tagsListView;
    private ListView commentListView;
    private Label syncStatusLabel;

    private readonly List<string> displayedComments = new();
    private readonly List<string> displayedTags = new();
    private readonly List<BlogPostHeader> blogPostHeaders = new();
    
    private long totalCommentPartitionCount = 0;
    private int currentCommentPartitionIndex = -1;
    
    private long totalTagPartitionCount = 0;
    private int currentTagPartitionIndex = -1;
    
    private sealed record BlogPostHeader(Guid Id, string Title);

    private sealed class DvvStateDto
    {
        public Dictionary<string, long> Versions { get; set; } = new();
        public Dictionary<string, HashSet<long>> Dots { get; set; } = new();
    }

    public UiService(IServiceProvider serviceProvider, List<string> replicaIds, List<Guid> blogPostIds)
    {
        this.serviceProvider = serviceProvider;
        this.replicaIds = replicaIds;
        this.blogPostIds = blogPostIds;

        foreach (var rId in replicaIds)
        {
            replicaDvvs[rId] = new DottedVersionVector();
        }

        LoadReplicaStates();
    }

    private void SaveReplicaStates()
    {
        try
        {
            var dtos = replicaDvvs.ToDictionary(
                kvp => kvp.Key,
                kvp => new DvvStateDto
                {
                    Versions = new Dictionary<string, long>(kvp.Value.Versions),
                    Dots = kvp.Value.Dots.ToDictionary(d => d.Key, d => new HashSet<long>(d.Value))
                });
            
            var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DvvStateFilePath, json);
        }
        catch 
        {
            // Ignore for showcase
        }
    }

    private void LoadReplicaStates()
    {
        try
        {
            if (File.Exists(DvvStateFilePath))
            {
                var json = File.ReadAllText(DvvStateFilePath);
                var dtos = JsonSerializer.Deserialize<Dictionary<string, DvvStateDto>>(json);
                if (dtos != null)
                {
                    foreach (var kvp in dtos)
                    {
                        var versions = kvp.Value.Versions;
                        var dots = kvp.Value.Dots.ToDictionary(d => d.Key, d => (ISet<long>)d.Value);
                        if (replicaDvvs.ContainsKey(kvp.Key))
                        {
                            replicaDvvs[kvp.Key] = new DottedVersionVector(versions, dots);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore for showcase, will just start fresh
        }
    }

    public void Run()
    {
        Application.Init();

        var top = Application.Top;

        topPane = new Window("Larger Than Memory CRDT Showcase")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        
        var menu = new MenuBar(new[]
        {
            new MenuBarItem("_File", new[]
            {
                new MenuItem("_Quit", "", () => Application.RequestStop())
            }),
            new MenuBarItem("_Actions", new[]
            {
                new MenuItem("_New Post", "", ShowNewPostDialog),
                new MenuItem("Add _Comment", "", ShowAddCommentDialog),
                new MenuItem("Add _Tag", "", ShowAddTagDialog),
                new MenuItem("_Sync Replicas", "", SyncReplicas),
                new MenuItem("Sync _Status", "", ShowSyncStatusDialog)
            }),
            new MenuBarItem("_Replica", CreateReplicaMenuItems())
        });

        var leftPane = new FrameView("Blog Posts")
        {
            X = 0,
            Y = 1,
            Width = 40,
            Height = Dim.Fill(1)
        };

        postListView = new ListView(new List<string>())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2)
        };
        postListView.SelectedItemChanged += OnPostSelected;

        syncStatusLabel = new Label("Sync: Up to date")
        {
            X = 0,
            Y = Pos.Bottom(postListView),
            Width = Dim.Fill()
        };

        leftPane.Add(postListView, syncStatusLabel);

        rightPane = new FrameView("Selected Post")
        {
            X = 40,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };

        var postContentLabel = new Label("Content:") { X = 0, Y = 0 };
        postContentView = new TextView()
        {
            X = 0,
            Y = Pos.Bottom(postContentLabel),
            Width = Dim.Fill(),
            Height = Dim.Percent(30),
            ReadOnly = true
        };

        var tagsLabel = new Label("Tags (Loaded On-Demand):") { X = 0, Y = Pos.Bottom(postContentView) + 1 };
        tagsListView = new ListView(new List<string>())
        {
            X = 0,
            Y = Pos.Bottom(tagsLabel),
            Width = Dim.Fill(),
            Height = Dim.Percent(20),
        };

        var commentsLabel = new Label("Comments (Loaded On-Demand):") { X = 0, Y = Pos.Bottom(tagsListView) + 1 };
        commentListView = new ListView(new List<string>())
        {
            X = 0,
            Y = Pos.Bottom(commentsLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        rightPane.Add(postContentLabel, postContentView, tagsLabel, tagsListView, commentsLabel, commentListView);

        var statusBar = new StatusBar(new[]
        {
            new StatusItem(Key.F2, "~F2~ Load Next Partitions", LoadNextPartitions),
            new StatusItem(Key.F5, "~F5~ Sync Changes", SyncReplicas),
            new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Quit", () => Application.RequestStop())
        });

        topPane.Add(leftPane, rightPane);
        top.Add(topPane, menu, statusBar);

        SwitchReplica(replicaIds.First());

        Application.Run();
        Application.Shutdown();
        currentScope?.Dispose();
    }

    private MenuItem[] CreateReplicaMenuItems()
    {
        return replicaIds.Select(id => new MenuItem($"View {id}", "", () => SwitchReplica(id))).ToArray();
    }

    private void SafeSetListViewSource(ListView listView, IEnumerable<string> source)
    {
        if (listView == null || source == null) return;
        
        var list = source.Select(s => s?.Replace("\r", "")?.Replace("\n", " ") ?? "").ToList();
        
        int maxLen = list.Count > 0 ? list.Max(s => s.Length) : 0;
        if (maxLen > 0)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Length < maxLen)
                {
                    list[i] = list[i].PadRight(maxLen);
                }
            }
        }
        
        listView.SetSource(list);
        
        if (listView.TopItem >= list.Count)
            listView.TopItem = Math.Max(0, list.Count - 1);
            
        if (listView.LeftItem > maxLen)
            listView.LeftItem = Math.Max(0, maxLen - 1);
            
        if (listView.SelectedItem >= list.Count)
            listView.SelectedItem = Math.Max(0, list.Count - 1);
            
        listView.SetNeedsDisplay();
    }

    private void SwitchReplica(string replicaId)
    {
        currentReplicaId = replicaId;
        currentScope?.Dispose();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        
        currentScope = scopeFactory.CreateScope(replicaId, replicaDvvs[replicaId]);
        partitionManager = currentScope.ServiceProvider.GetRequiredService<IPartitionManager<BlogPost>>();
        
        if (topPane is not null)
        {
            topPane.Title = $"CRDT Showcase - Viewing: {replicaId}";
        }
        
        UpdateSyncStatusUI();
        LoadBlogPostHeadersAsync();
    }

    private void UpdateSyncStatusUI()
    {
        Application.MainLoop.Invoke(() => 
        {
            var vvSyncService = serviceProvider.GetRequiredService<IVersionVectorSyncService>();
            var targetCtx = new ReplicaContext { ReplicaId = currentReplicaId, GlobalVersionVector = replicaDvvs[currentReplicaId] };
            
            int totalMissing = 0;
            foreach (var source in replicaIds.Where(r => r != currentReplicaId))
            {
                var sourceCtx = new ReplicaContext { ReplicaId = source, GlobalVersionVector = replicaDvvs[source] };
                var req = vvSyncService.CalculateRequirement(targetCtx, sourceCtx);
                if (req.IsBehind && req.RequirementsByOrigin != null)
                {
                    totalMissing += (int)req.RequirementsByOrigin.Values.Sum(r => Math.Max(0, r.SourceContiguousVersion - r.TargetContiguousVersion) + (r.SourceMissingDots?.Count ?? 0));
                }
            }
            
            if (totalMissing > 0)
            {
                syncStatusLabel.Text = $"⚠ Behind by {totalMissing} operations";
            }
            else
            {
                syncStatusLabel.Text = $"✔ Up to date";
            }
        });
    }

    private void ShowSyncStatusDialog()
    {
        var vvSyncService = serviceProvider.GetRequiredService<IVersionVectorSyncService>();
        
        var dialog = new Dialog("Global Sync Status", 70, 20);
        var listView = new ListView(new List<string>())
        {
            X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill(2)
        };
        
        var statusLines = new List<string>();
        
        foreach (var target in replicaIds)
        {
            var targetCtx = new ReplicaContext { ReplicaId = target, GlobalVersionVector = replicaDvvs[target] };
            
            foreach (var source in replicaIds)
            {
                if (target == source) continue;
                
                var sourceCtx = new ReplicaContext { ReplicaId = source, GlobalVersionVector = replicaDvvs[source] };
                var req = vvSyncService.CalculateRequirement(targetCtx, sourceCtx);
                
                if (req.IsBehind && req.RequirementsByOrigin != null)
                {
                    var missingCount = req.RequirementsByOrigin.Values.Sum(r => Math.Max(0, r.SourceContiguousVersion - r.TargetContiguousVersion) + (r.SourceMissingDots?.Count ?? 0));
                    statusLines.Add($"{target} is BEHIND {source} by {missingCount} ops");
                }
            }
        }
        
        if (statusLines.Count == 0)
        {
            statusLines.Add("All replicas are fully synchronized!");
        }
        
        SafeSetListViewSource(listView, statusLines);
        
        var btnOk = new Button("Close", is_default: true);
        btnOk.Clicked += () => Application.RequestStop();
        
        dialog.Add(listView);
        dialog.AddButton(btnOk);
        Application.Run(dialog);
    }

    private async void LoadBlogPostHeadersAsync()
    {
        if (partitionManager is null || blogPostIds is null) return;

        Application.MainLoop.Invoke(() =>
        {
            postListView.SelectedItemChanged -= OnPostSelected;
            SafeSetListViewSource(postListView, new List<string> { "Loading..." });
            postListView.SelectedItemChanged += OnPostSelected;

            rightPane.Title = "Selected Post";
            postContentView.Text = " ";
            displayedTags.Clear();
            SafeSetListViewSource(tagsListView, new List<string>());
            displayedComments.Clear();
            SafeSetListViewSource(commentListView, new List<string>());
        });

        var uniqueKeys = await partitionManager.GetAllLogicalKeysAsync();
        
        var newHeaders = new List<BlogPostHeader>();
        foreach(var key in uniqueKeys.Cast<Guid>()) {
            if (!blogPostIds.Contains(key)) {
                blogPostIds.Add(key);
            }
        }

        foreach (var id in blogPostIds)
        {
            var content = await partitionManager.GetHeaderPartitionContentAsync(id);
            if (content.HasValue)
            {
                newHeaders.Add(new BlogPostHeader(content.Value.Data.Id, content.Value.Data.Title ?? "Untitled"));
            }
        }

        Application.MainLoop.Invoke(() =>
        {
            postListView.SelectedItemChanged -= OnPostSelected;

            blogPostHeaders.Clear();
            blogPostHeaders.AddRange(newHeaders);

            SafeSetListViewSource(postListView, blogPostHeaders.Select(h => h.Title).ToList());
            
            var index = blogPostHeaders.FindIndex(h => h.Id == selectedBlogPostId);
            if (index >= 0 && index < postListView.Source.Count) {
                postListView.SelectedItem = index;
            } else if (postListView.Source?.Count > 0) {
                postListView.SelectedItem = 0;
            }

            postListView.SelectedItemChanged += OnPostSelected;

            if (postListView.Source?.Count > 0)
            {
                LoadPostDetails(postListView.SelectedItem);
            }
        });
    }

    private void OnPostSelected(ListViewItemEventArgs args)
    {
        LoadPostDetails(args.Item);
    }

    private async void LoadPostDetails(int itemIndex)
    {
        if (partitionManager is null || commentListView is null || itemIndex < 0 || blogPostHeaders is null || itemIndex >= blogPostHeaders.Count)
        {
            Application.MainLoop.Invoke(() => 
            {
                rightPane.Title = "Selected Post";
                postContentView.Text = " ";
                displayedTags.Clear();
                SafeSetListViewSource(tagsListView, new List<string>());
                displayedComments.Clear();
                SafeSetListViewSource(commentListView, new List<string>());
            });
            return;
        }

        var selectedHeader = blogPostHeaders[itemIndex];
        selectedBlogPostId = selectedHeader.Id;

        var headerContent = await partitionManager.GetHeaderPartitionContentAsync(selectedBlogPostId);
        var tagCount = await partitionManager.GetDataPartitionCountAsync(selectedBlogPostId, TagsPropertyName);
        var commentCount = await partitionManager.GetDataPartitionCountAsync(selectedBlogPostId, CommentsPropertyName);

        Application.MainLoop.Invoke(() =>
        {
            if (headerContent.HasValue)
            {
                var post = headerContent.Value.Data;
                rightPane.Title = post.Title ?? "Unknown";
                postContentView.Text = string.IsNullOrEmpty(post.Content) ? " " : post.Content;
            }

            totalTagPartitionCount = tagCount;
            currentTagPartitionIndex = (int)totalTagPartitionCount;
            displayedTags.Clear();
            SafeSetListViewSource(tagsListView, new List<string>());

            totalCommentPartitionCount = commentCount;
            currentCommentPartitionIndex = (int)totalCommentPartitionCount;
            displayedComments.Clear();
            SafeSetListViewSource(commentListView, new List<string>());
            
            LoadNextPartitions();
        });
    }

    private void LoadNextPartitions()
    {
        LoadNextTagPartition();
        LoadNextCommentPartition();
    }

    private async void LoadNextTagPartition()
    {
        if (currentTagPartitionIndex - 1 < 0) return;

        currentTagPartitionIndex--;
        var indexToLoad = currentTagPartitionIndex;
        var partition = await partitionManager.GetDataPartitionByIndexAsync(selectedBlogPostId, indexToLoad, TagsPropertyName);

        if (partition is null) return;

        var content = await partitionManager.GetDataPartitionContentAsync(partition.GetPartitionKey(), TagsPropertyName);

        Application.MainLoop.Invoke(() =>
        {
            if (content.HasValue && content.Value.Data?.Tags is { Count: > 0 } tags)
            {
                var scrollToIndex = displayedTags.Count;
                
                displayedTags.Add($"--- Partition {indexToLoad + 1}/{totalTagPartitionCount} ({tags.Count} tags) ---");
                displayedTags.AddRange(tags);

                SafeSetListViewSource(tagsListView, displayedTags);

                if (scrollToIndex < tagsListView.Source.Count)
                {
                    try { tagsListView.TopItem = scrollToIndex; } catch {}
                    try { tagsListView.SelectedItem = scrollToIndex; } catch {}
                }
            }
        });
    }

    private async void LoadNextCommentPartition()
    {
        if (currentCommentPartitionIndex - 1 < 0) return;

        currentCommentPartitionIndex--;
        var indexToLoad = currentCommentPartitionIndex;
        var partition = await partitionManager.GetDataPartitionByIndexAsync(selectedBlogPostId, indexToLoad, CommentsPropertyName);

        if (partition is null) return;

        var content = await partitionManager.GetDataPartitionContentAsync(partition.GetPartitionKey(), CommentsPropertyName);

        Application.MainLoop.Invoke(() =>
        {
            if (content.HasValue && content.Value.Data?.Comments is { Count: > 0 } comments)
            {
                var scrollToIndex = displayedComments.Count;
                var newComments = comments.Values
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => $"[{c.CreatedAt:g}] {c.Author}: {c.Text}")
                    .ToList();
                
                displayedComments.Add($"--- Partition {indexToLoad + 1}/{totalCommentPartitionCount} ({comments.Count} comments) ---");
                displayedComments.AddRange(newComments);

                SafeSetListViewSource(commentListView, displayedComments);

                if (scrollToIndex < commentListView.Source.Count)
                {
                    try { commentListView.TopItem = scrollToIndex; } catch {}
                    try { commentListView.SelectedItem = scrollToIndex; } catch {}
                }
            }
        });
    }

    private void ShowNewPostDialog()
    {
        var dialog = new Dialog("Create New Post", 60, 14);
        var titleLabel = new Label("Title:") { X = 1, Y = 1 };
        var titleText = new TextField("") { X = 10, Y = 1, Width = 40 };
        var contentLabel = new Label("Content:") { X = 1, Y = 3 };
        var contentText = new TextView() { X = 10, Y = 3, Width = 40, Height = 4 };

        var btnOk = new Button("Create", is_default: true);
        var btnCancel = new Button("Cancel");

        btnOk.Clicked += async () => {
            Application.RequestStop();
            var title = titleText.Text?.ToString();
            var content = contentText.Text?.ToString();
            if (string.IsNullOrWhiteSpace(title)) return;

            var id = Guid.NewGuid();
            var newPost = new BlogPost { Id = id, Title = title, Content = content ?? "" };
            
            await partitionManager.InitializeAsync(new BlogPost { Id = id });
            var emptyDoc = await partitionManager.GetHeaderPartitionContentAsync(id);
            var patcher = currentScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
            var applicator = currentScope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
            
            var patch = patcher.GeneratePatch(emptyDoc.Value, newPost);
            
            // Operations generated during patch apply will be captured automatically by the JournalingApplicatorDecorator
            await applicator.ApplyPatchAsync(emptyDoc.Value, patch);

            Application.MainLoop.Invoke(() => {
                SaveReplicaStates();
                if (!blogPostIds.Contains(id)) {
                    blogPostIds.Add(id);
                }
                UpdateSyncStatusUI();
                LoadBlogPostHeadersAsync();
            });
        };
        btnCancel.Clicked += () => Application.RequestStop();

        dialog.AddButton(btnOk);
        dialog.AddButton(btnCancel);
        dialog.Add(titleLabel, titleText, contentLabel, contentText);
        Application.Run(dialog);
    }

    private void ShowAddCommentDialog()
    {
        if (selectedBlogPostId == Guid.Empty) {
            MessageBox.ErrorQuery("Error", "Please select a post first.", "Ok");
            return;
        }

        var dialog = new Dialog("Add Comment", 50, 10);
        var authorLabel = new Label("Author:") { X = 1, Y = 1 };
        var authorText = new TextField("User") { X = 10, Y = 1, Width = 30 };
        var contentLabel = new Label("Text:") { X = 1, Y = 3 };
        var contentText = new TextField("") { X = 10, Y = 3, Width = 30 };

        var btnOk = new Button("Add", is_default: true);
        var btnCancel = new Button("Cancel");

        btnOk.Clicked += async () => {
            Application.RequestStop();
            var author = authorText.Text?.ToString();
            var content = contentText.Text?.ToString();
            if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(content)) return;

            var comment = new Comment(Guid.NewGuid(), author, content, DateTimeOffset.UtcNow);
            
            var headerContent = await partitionManager.GetHeaderPartitionContentAsync(selectedBlogPostId);
            var patcher = currentScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
            var applicator = currentScope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
            
            var fromDoc = new CrdtDocument<BlogPost>(
                new BlogPost { Id = selectedBlogPostId, Comments = new Dictionary<DateTimeOffset, Comment>() }, 
                headerContent.Value.Metadata);
            
            var operation = patcher.GenerateOperation(fromDoc, x => x.Comments, new MapSetIntent(comment.CreatedAt, comment));
            var patch = new CrdtPatch(new[] { operation });
            
            // Journal decorator automatically saves
            await applicator.ApplyPatchAsync(fromDoc, patch);

            var commentCount = await partitionManager.GetDataPartitionCountAsync(selectedBlogPostId, CommentsPropertyName);
            
            Application.MainLoop.Invoke(() => {
                SaveReplicaStates();
                totalCommentPartitionCount = commentCount;
                currentCommentPartitionIndex = (int)totalCommentPartitionCount;
                displayedComments.Clear();
                UpdateSyncStatusUI();
                LoadNextCommentPartition();
            });
        };
        btnCancel.Clicked += () => Application.RequestStop();

        dialog.AddButton(btnOk);
        dialog.AddButton(btnCancel);
        dialog.Add(authorLabel, authorText, contentLabel, contentText);
        Application.Run(dialog);
    }

    private void ShowAddTagDialog()
    {
        if (selectedBlogPostId == Guid.Empty) {
            MessageBox.ErrorQuery("Error", "Please select a post first.", "Ok");
            return;
        }

        var dialog = new Dialog("Add Tag", 40, 8);
        var tagLabel = new Label("Tag:") { X = 1, Y = 1 };
        var tagText = new TextField("") { X = 6, Y = 1, Width = 20 };

        var btnOk = new Button("Add", is_default: true);
        var btnCancel = new Button("Cancel");

        btnOk.Clicked += async () => {
            Application.RequestStop();
            var newTag = tagText.Text?.ToString();
            if (string.IsNullOrWhiteSpace(newTag)) return;

            var headerContent = await partitionManager.GetHeaderPartitionContentAsync(selectedBlogPostId);
            var patcher = currentScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
            var applicator = currentScope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
            
            var header = headerContent.Value.Data;
            var fromDoc = new CrdtDocument<BlogPost>(header, headerContent.Value.Metadata);
            
            var operation = patcher.GenerateOperation(fromDoc, x => x.Tags, new AddIntent(newTag));
            var patch = new CrdtPatch(new[] { operation });
            
            // Journal decorator automatically saves
            await applicator.ApplyPatchAsync(fromDoc, patch);

            Application.MainLoop.Invoke(() => {
                SaveReplicaStates();
                UpdateSyncStatusUI();
                var index = blogPostHeaders.FindIndex(h => h.Id == selectedBlogPostId);
                if (index >= 0) {
                    LoadPostDetails(index);
                }
            });
        };
        btnCancel.Clicked += () => Application.RequestStop();

        dialog.AddButton(btnOk);
        dialog.AddButton(btnCancel);
        dialog.Add(tagLabel, tagText);
        Application.Run(dialog);
    }

    private async void SyncReplicas()
    {
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        var vvSyncService = serviceProvider.GetRequiredService<IVersionVectorSyncService>();
        int syncCount = 0;

        foreach (var replica in replicaIds.Where(r => r != currentReplicaId))
        {
            var targetCtx = new ReplicaContext { ReplicaId = currentReplicaId, GlobalVersionVector = replicaDvvs[currentReplicaId] };
            var sourceCtx = new ReplicaContext { ReplicaId = replica, GlobalVersionVector = replicaDvvs[replica] };
            
            var req = vvSyncService.CalculateRequirement(targetCtx, sourceCtx);
            if (!req.IsBehind) continue;

            // Fetch missing operations directly from the source replica's journal
            using var sourceScope = scopeFactory.CreateScope(replica, sourceCtx.GlobalVersionVector);
            var sourceJournalManager = sourceScope.ServiceProvider.GetRequiredService<IJournalManager>();
            var missingOpsStream = sourceJournalManager.GetMissingOperationsAsync(req);

            var opsByDocument = new Dictionary<string, List<CrdtOperation>>();
            await foreach (var jOp in missingOpsStream)
            {
                if (!opsByDocument.TryGetValue(jOp.DocumentId, out var opList))
                {
                    opList = new List<CrdtOperation>();
                    opsByDocument[jOp.DocumentId] = opList;
                }
                opList.Add(jOp.Operation);
            }

            if (opsByDocument.Count > 0)
            {
                var keys = new HashSet<IComparable>(await partitionManager.GetAllLogicalKeysAsync());
                var targetApplicator = currentScope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();

                foreach (var kvp in opsByDocument)
                {
                    if (!Guid.TryParse(kvp.Key, out var logicalKey)) continue;

                    if (!keys.Contains(logicalKey))
                    {
                        await partitionManager.InitializeAsync(new BlogPost { Id = logicalKey });
                        keys.Add(logicalKey);
                    }
                    
                    var headerDoc = await partitionManager.GetHeaderPartitionContentAsync(logicalKey);
                    var patch = new CrdtPatch(kvp.Value);
                    await targetApplicator.ApplyPatchAsync(headerDoc.Value, patch);
                    syncCount++;
                }
            }
        }

        Application.MainLoop.Invoke(() => {
            SaveReplicaStates();
            MessageBox.Query("Sync Complete", $"Successfully applied patches for {syncCount} documents.", "Ok");
            UpdateSyncStatusUI();
            LoadBlogPostHeadersAsync();
        });
    }
}