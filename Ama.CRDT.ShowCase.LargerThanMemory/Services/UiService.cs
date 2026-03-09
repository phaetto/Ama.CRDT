namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
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
        tagsListView = new ListView()
        {
            X = 0,
            Y = Pos.Bottom(tagsLabel),
            Width = Dim.Fill(),
            Height = Dim.Percent(20),
        };

        var commentsLabel = new Label("Comments (Loaded On-Demand):") { X = 0, Y = Pos.Bottom(tagsListView) + 1 };
        commentListView = new ListView()
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

    private void SwitchReplica(string replicaId)
    {
        currentReplicaId = replicaId;
        currentScope?.Dispose();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        
        // Feed the specific DVV instance into the scope so that the library manages the increments internally
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
                if (req.IsBehind)
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
        var listView = new ListView()
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
                
                if (req.IsBehind)
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
        
        listView.SetSource(statusLines);
        
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
            postListView.SetSource(new List<string> { "Loading..." });
            rightPane.Title = "Selected Post";
            postContentView.Text = "";
            displayedTags.Clear();
            tagsListView.SetSource(displayedTags);
            displayedComments.Clear();
            commentListView.SetSource(displayedComments);
        });

        blogPostHeaders.Clear();
        var uniqueKeys = await partitionManager.GetAllLogicalKeysAsync();
        
        // Add new ids not found in the original list (received from sync)
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
                blogPostHeaders.Add(new BlogPostHeader(content.Value.Data.Id, content.Value.Data.Title));
            }
        }

        Application.MainLoop.Invoke(() =>
        {
            postListView.SetSource(blogPostHeaders.Select(h => h.Title).ToList());
            
            // Re-select currently selected post if available
            var index = blogPostHeaders.FindIndex(h => h.Id == selectedBlogPostId);
            if (index >= 0 && index < postListView.Source.Count) {
                postListView.SelectedItem = index;
            }
        });
    }

    private async void OnPostSelected(ListViewItemEventArgs args)
    {
        if (partitionManager is null || commentListView is null || args.Item < 0 || blogPostHeaders is null || args.Item >= blogPostHeaders.Count)
        {
            rightPane.Title = "Selected Post";
            postContentView.Text = "";
            displayedTags.Clear();
            tagsListView.SetSource(displayedTags);
            displayedComments.Clear();
            commentListView.SetSource(displayedComments);
            return;
        }

        var selectedHeader = blogPostHeaders[args.Item];
        selectedBlogPostId = selectedHeader.Id;

        var headerContent = await partitionManager.GetHeaderPartitionContentAsync(selectedBlogPostId);

        if (headerContent.HasValue)
        {
            var post = headerContent.Value.Data;
            rightPane.Title = post.Title;
            postContentView.Text = post.Content;
        }

        totalTagPartitionCount = await partitionManager.GetDataPartitionCountAsync(selectedBlogPostId, TagsPropertyName);
        currentTagPartitionIndex = (int)totalTagPartitionCount;
        displayedTags.Clear();
        tagsListView.SetSource(displayedTags);

        totalCommentPartitionCount = await partitionManager.GetDataPartitionCountAsync(selectedBlogPostId, CommentsPropertyName);
        currentCommentPartitionIndex = (int)totalCommentPartitionCount;
        displayedComments.Clear();
        commentListView.SetSource(displayedComments);
        
        LoadNextPartitions();
    }

    private void LoadNextPartitions()
    {
        LoadNextTagPartition();
        LoadNextCommentPartition();
    }

    private async void LoadNextTagPartition()
    {
        if (currentTagPartitionIndex - 1 < 0)
        {
            return;
        }

        currentTagPartitionIndex--;
        var partition = await partitionManager.GetDataPartitionByIndexAsync(selectedBlogPostId, currentTagPartitionIndex, TagsPropertyName);

        if (partition is null)
        {
            MessageBox.ErrorQuery("Error", $"Could not load tag partition at index {currentTagPartitionIndex}. The index might be out of sync.", "Ok");
            return;
        }

        var content = await partitionManager.GetDataPartitionContentAsync(partition.GetPartitionKey(), TagsPropertyName);

        if (content.HasValue && content.Value.Data?.Tags is { Count: > 0 } tags)
        {
            var scrollToIndex = displayedTags.Count;
            
            displayedTags.Add($"--- Partition {currentTagPartitionIndex + 1}/{totalTagPartitionCount} ({tags.Count} tags) ---");
            displayedTags.AddRange(tags);

            tagsListView.SetSource(displayedTags);

            if (scrollToIndex < tagsListView.Source.Count)
            {
                tagsListView.TopItem = scrollToIndex;
                tagsListView.SelectedItem = scrollToIndex;
            }
        }
    }

    private async void LoadNextCommentPartition()
    {
        if (currentCommentPartitionIndex - 1 < 0)
        {
            return;
        }

        currentCommentPartitionIndex--;
        var partition = await partitionManager.GetDataPartitionByIndexAsync(selectedBlogPostId, currentCommentPartitionIndex, CommentsPropertyName);

        if (partition is null)
        {
            MessageBox.ErrorQuery("Error", $"Could not load comment partition at index {currentCommentPartitionIndex}. The index might be out of sync.", "Ok");
            return;
        }

        var content = await partitionManager.GetDataPartitionContentAsync(partition.GetPartitionKey(), CommentsPropertyName);

        if (content.HasValue && content.Value.Data?.Comments is { Count: > 0 } comments)
        {
            var scrollToIndex = displayedComments.Count;
            var newComments = comments.Values
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => $"[{c.CreatedAt:g}] {c.Author}: {c.Text}")
                .ToList();
            
            displayedComments.Add($"--- Partition {currentCommentPartitionIndex + 1}/{totalCommentPartitionCount} ({comments.Count} comments) ---");
            displayedComments.AddRange(newComments);

            commentListView.SetSource(displayedComments);

            if (scrollToIndex < commentListView.Source.Count)
            {
                commentListView.TopItem = scrollToIndex;
                commentListView.SelectedItem = scrollToIndex;
            }
        }
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
            
            var patch = patcher.GeneratePatch(emptyDoc.Value, newPost);
            patch = patch with { LogicalKey = id };
            await partitionManager.ApplyPatchAsync(patch);

            var syncService = serviceProvider.GetRequiredService<SyncService>();
            syncService.QueuePatch(currentReplicaId, patch, replicaIds);

            // The scope components are handling the DVV increments, we just persist
            SaveReplicaStates();

            Application.MainLoop.Invoke(() => {
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
            
            var fromDoc = new CrdtDocument<BlogPost>(
                new BlogPost { Id = selectedBlogPostId, Comments = new Dictionary<DateTimeOffset, Comment>() }, 
                headerContent.Value.Metadata);
            
            var operation = patcher.BuildOperation(fromDoc, x => x.Comments).Set(comment.CreatedAt, comment);
            var patch = new CrdtPatch(new[] { operation }) { LogicalKey = selectedBlogPostId };
            
            await partitionManager.ApplyPatchAsync(patch);

            var syncService = serviceProvider.GetRequiredService<SyncService>();
            syncService.QueuePatch(currentReplicaId, patch, replicaIds);

            // The scope components are handling the DVV increments, we just persist
            SaveReplicaStates();

            totalCommentPartitionCount = await partitionManager.GetDataPartitionCountAsync(selectedBlogPostId, CommentsPropertyName);
            currentCommentPartitionIndex = (int)totalCommentPartitionCount;
            
            Application.MainLoop.Invoke(() => {
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
            
            var header = headerContent.Value.Data;
            var fromDoc = new CrdtDocument<BlogPost>(header, headerContent.Value.Metadata);
            
            var operation = patcher.BuildOperation(fromDoc, x => x.Tags).Add(newTag);
            var patch = new CrdtPatch(new[] { operation }) { LogicalKey = selectedBlogPostId };
            
            await partitionManager.ApplyPatchAsync(patch);

            var syncService = serviceProvider.GetRequiredService<SyncService>();
            syncService.QueuePatch(currentReplicaId, patch, replicaIds);

            // The scope components are handling the DVV increments, we just persist
            SaveReplicaStates();

            Application.MainLoop.Invoke(() => {
                UpdateSyncStatusUI();
                var index = blogPostHeaders.FindIndex(h => h.Id == selectedBlogPostId);
                if (index >= 0) {
                    OnPostSelected(new ListViewItemEventArgs(index, null));
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
        var syncService = serviceProvider.GetRequiredService<SyncService>();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        int syncCount = 0;

        foreach (var replica in replicaIds.Where(r => r != currentReplicaId))
        {
            // By passing the replica's specific DVV into the scope factory, 
            // the PartitionManager and Applicator will increment/update the DVV for us automatically during ApplyPatchAsync.
            using var scope = scopeFactory.CreateScope(replica, replicaDvvs[replica]);
            var targetPartitionManager = scope.ServiceProvider.GetRequiredService<IPartitionManager<BlogPost>>();
            
            while (syncService.TryDequeue(replica, out var patch))
            {
                var keys = await targetPartitionManager.GetAllLogicalKeysAsync();
                if (!keys.Contains((Guid)patch.LogicalKey))
                {
                    await targetPartitionManager.InitializeAsync(new BlogPost { Id = (Guid)patch.LogicalKey });
                }
                await targetPartitionManager.ApplyPatchAsync(patch);
                syncCount++;
            }
        }

        SaveReplicaStates();

        Application.MainLoop.Invoke(() => {
            MessageBox.Query("Sync Complete", $"Successfully synced {syncCount} patches to other replicas.", "Ok");
            UpdateSyncStatusUI();
            LoadBlogPostHeadersAsync();
        });
    }
}