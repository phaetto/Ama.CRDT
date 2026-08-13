namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Journaling;
using Ama.CRDT.Services.LargerThanMemory;
using Ama.CRDT.Services.Versioning;
using Ama.CRDT.ShowCase.LargerThanMemory.Models;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;

public sealed class UiService
{
    private const string CommentsPropertyName = nameof(BlogPost.Comments);
    private const string TagsPropertyName = nameof(BlogPost.Tags);
    public const string DvvStateFilePath = "replica_dvvs.json";

    private readonly IServiceProvider serviceProvider;
    private readonly List<string> replicaIds;
    private readonly List<Guid> blogPostIds;
    private Guid selectedBlogPostId;

    private IServiceScope currentScope;
    private IVirtualDocumentCollectionReader<BlogPost> documentCollection;
    private IChunkDocumentManager<BlogPost> chunkManager;
    private string currentReplicaId;
    
    // Session tracking to prevent background tasks from clashing when switching posts quickly
    private Guid currentPostSessionId = Guid.Empty;

    // Track simulated Global Version Vectors to show causality gaps
    private readonly Dictionary<string, DottedVersionVector> replicaDvvs;

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
    
    private long totalCommentCount = -1;
    private long totalTagCount = -1;
    
    private IAsyncEnumerator<string>? tagEnumerator;
    private IAsyncEnumerator<KeyValuePair<DateTimeOffset, Comment>>? commentEnumerator;

    private bool isLoadingTags = false;
    private bool isLoadingComments = false;
    
    private sealed record BlogPostHeader(Guid Id, string Title);

    public sealed class DvvStateDto
    {
        public Dictionary<string, long> Versions { get; set; } = new();
        public Dictionary<string, HashSet<long>> Dots { get; set; } = new();
    }

    public UiService(IServiceProvider serviceProvider, List<string> replicaIds, List<Guid> blogPostIds, Dictionary<string, DottedVersionVector> replicaDvvs)
    {
        this.serviceProvider = serviceProvider;
        this.replicaIds = replicaIds;
        this.blogPostIds = blogPostIds;
        this.replicaDvvs = replicaDvvs;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
    public static Dictionary<string, DottedVersionVector> LoadReplicaStates(IEnumerable<string> replicaIds)
    {
        var replicaDvvs = new Dictionary<string, DottedVersionVector>();
        foreach (var rId in replicaIds)
        {
            replicaDvvs[rId] = new DottedVersionVector();
        }

        try
        {
            if (File.Exists(DvvStateFilePath))
            {
                var json = File.ReadAllText(DvvStateFilePath);
                var options = new JsonSerializerOptions 
                { 
                    TypeInfoResolver = LargerThanMemoryJsonContext.Default
                };
                var dtos = (Dictionary<string, DvvStateDto>?)JsonSerializer.Deserialize(json, typeof(Dictionary<string, DvvStateDto>), options);
                
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

        return replicaDvvs;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
    public static void SaveReplicaStates(Dictionary<string, DottedVersionVector> replicaDvvs)
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
            
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                TypeInfoResolver = LargerThanMemoryJsonContext.Default
            };
            var json = JsonSerializer.Serialize(dtos, options);
            File.WriteAllText(DvvStateFilePath, json);
        }
        catch 
        {
            // Ignore for showcase
        }
    }

    private void SaveReplicaStates()
    {
        SaveReplicaStates(this.replicaDvvs);
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
                new MenuItem("Sync _Current Replica", "", SyncReplicas),
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
            new StatusItem(Key.F2, "~F2~ Load More Data", LoadMoreData),
            new StatusItem(Key.F5, "~F5~ Sync Current Replica", SyncReplicas),
            new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Quit", () => Application.RequestStop())
        });

        topPane.Add(leftPane, rightPane);
        top.Add(topPane, menu, statusBar);

        SwitchReplica(replicaIds.First());

        Application.Run();
        Application.Shutdown();
        
        _ = DisposeEnumeratorsAsync();
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
        currentPostSessionId = Guid.NewGuid(); // Cancel pending backgrounds

        // Ensure dependency injection and scopes are resolved synchronously on the UI thread
        // prior to allowing background tasks to attempt using them to prevent race condition NREs.
        currentScope?.Dispose();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        
        currentScope = scopeFactory.CreateScope(replicaId, replicaDvvs[replicaId]);
        documentCollection = currentScope.ServiceProvider.GetRequiredService<IVirtualDocumentCollectionReader<BlogPost>>();
        chunkManager = currentScope.ServiceProvider.GetRequiredService<IChunkDocumentManager<BlogPost>>();
        
        if (topPane is not null)
        {
            topPane.Title = $"CRDT Showcase - Viewing: {replicaId}";
        }
        
        Task.Run(async () =>
        {
            try
            {
                await DisposeEnumeratorsAsync().ConfigureAwait(false);
                UpdateSyncStatusUI();
                await LoadBlogPostHeadersAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() => MessageBox.ErrorQuery("Error", ex.Message, "Ok"));
            }
        });
    }

    private void UpdateSyncStatusUI()
    {
        Application.MainLoop.Invoke(() => 
        {
            var vvSyncService = serviceProvider.GetRequiredService<IVersionVectorSyncService>();
            
            int totalMissing = 0;
            foreach (var source in replicaIds.Where(r => r != currentReplicaId))
            {
                var req = vvSyncService.CalculateRequirement(currentReplicaId, replicaDvvs[currentReplicaId], source, replicaDvvs[source]);
                if (req.IsBehind && req.RequirementsByOrigin != null)
                {
                    totalMissing += (int)req.RequirementsByOrigin.Values.Sum(r => Math.Max(0, r.SourceContiguousVersion - r.TargetContiguousVersion) + (r.SourceMissingDots?.Count ?? 0));
                }
            }
            
            if (totalMissing > 0)
            {
                syncStatusLabel.Text = $"⚠ {currentReplicaId} is behind by {totalMissing} operations";
            }
            else
            {
                syncStatusLabel.Text = $"✔ {currentReplicaId} is up to date";
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
            foreach (var source in replicaIds)
            {
                if (target == source) continue;
                
                var req = vvSyncService.CalculateRequirement(target, replicaDvvs[target], source, replicaDvvs[source]);
                
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

    private async Task LoadBlogPostHeadersAsync()
    {
        if (documentCollection is null || blogPostIds is null) return;

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

        var uniqueKeys = await documentCollection.GetAllLogicalKeysAsync().ConfigureAwait(false);
        
        var newHeaders = new List<BlogPostHeader>();
        
        lock (blogPostIds) 
        {
            foreach(var key in uniqueKeys.Cast<Guid>()) {
                if (!blogPostIds.Contains(key)) {
                    blogPostIds.Add(key);
                }
            }
        }

        foreach (var id in blogPostIds)
        {
            var post = await documentCollection.GetDocumentHeaderAsync(id).ConfigureAwait(false);
            if (post != null)
            {
                newHeaders.Add(new BlogPostHeader(post.Value.Data.Id, post.Value.Data.Title ?? "Untitled"));
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

    private void LoadPostDetails(int itemIndex)
    {
        if (documentCollection is null || commentListView is null || itemIndex < 0 || blogPostHeaders is null || itemIndex >= blogPostHeaders.Count)
        {
            currentPostSessionId = Guid.NewGuid();
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

        // Establish a unique session ID for this load to gracefully cancel pending background enumerators
        var sessionId = Guid.NewGuid();
        currentPostSessionId = sessionId;

        // Give immediate visual feedback that we are loading!
        Application.MainLoop.Invoke(() =>
        {
            rightPane.Title = "Loading...";
            postContentView.Text = "Loading...";
            displayedTags.Clear();
            SafeSetListViewSource(tagsListView, new List<string> { "Loading..." });
            displayedComments.Clear();
            SafeSetListViewSource(commentListView, new List<string> { "Loading..." });
        });

        Task.Run(async () => 
        {
            try
            {
                var post = await documentCollection.GetDocumentHeaderAsync(selectedBlogPostId).ConfigureAwait(false);

                if (currentPostSessionId != sessionId) return;

                Application.MainLoop.Invoke(() =>
                {
                    if (currentPostSessionId != sessionId) return;

                    if (post != null)
                    {
                        rightPane.Title = post.Value.Data.Title ?? "Unknown";
                        postContentView.Text = string.IsNullOrEmpty(post.Value.Data.Content) ? " " : post.Value.Data.Content;
                    }

                    // Clear the placeholders so LoadMore can populate them cleanly
                    displayedTags.Clear();
                    displayedComments.Clear();
                });

                if (currentPostSessionId != sessionId) return;

                await DisposeEnumeratorsAsync().ConfigureAwait(false);
                
                // Reset states
                isLoadingTags = false;
                isLoadingComments = false;
                totalTagCount = -1;
                totalCommentCount = -1;
                
                tagEnumerator = documentCollection.GetElementsAsync<string>(selectedBlogPostId, TagsPropertyName).GetAsyncEnumerator();
                commentEnumerator = documentCollection.GetElementsAsync<KeyValuePair<DateTimeOffset, Comment>>(selectedBlogPostId, CommentsPropertyName).GetAsyncEnumerator();

                // Fetch the first 10 items without blocking on total count (which requires scanning chunks)
                await LoadMoreTagsAsync(sessionId).ConfigureAwait(false);
                await LoadMoreCommentsAsync(sessionId).ConfigureAwait(false);
                
                if (currentPostSessionId != sessionId) return;

                // Now compute total counts in the background so the UI doesn't hang.
                var tagCount = await documentCollection.GetElementCountAsync(selectedBlogPostId, TagsPropertyName).ConfigureAwait(false);
                var commentCount = await documentCollection.GetElementCountAsync(selectedBlogPostId, CommentsPropertyName).ConfigureAwait(false);

                if (currentPostSessionId == sessionId)
                {
                    Application.MainLoop.Invoke(() => 
                    {
                        if (currentPostSessionId != sessionId) return;
                        
                        totalTagCount = tagCount;
                        totalCommentCount = commentCount;

                        if (displayedTags.Count > 0 && displayedTags[0].StartsWith("---"))
                        {
                            displayedTags[0] = $"--- Showing Tags ({totalTagCount} total) ---";
                            SafeSetListViewSource(tagsListView, displayedTags);
                        }
                        if (displayedComments.Count > 0 && displayedComments[0].StartsWith("---"))
                        {
                            displayedComments[0] = $"--- Showing Comments ({totalCommentCount} total) ---";
                            SafeSetListViewSource(commentListView, displayedComments);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // Only prompt if this session wasn't already deliberately canceled by a user click
                if (currentPostSessionId == sessionId) 
                {
                    Application.MainLoop.Invoke(() => MessageBox.ErrorQuery("Error", ex.Message, "Ok"));
                }
            }
        });
    }

    private void LoadMoreData()
    {
        var sessionId = currentPostSessionId;
        Task.Run(async () =>
        {
            try
            {
                await LoadMoreTagsAsync(sessionId).ConfigureAwait(false);
                await LoadMoreCommentsAsync(sessionId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (currentPostSessionId == sessionId)
                {
                    Application.MainLoop.Invoke(() => MessageBox.ErrorQuery("Error", ex.Message, "Ok"));
                }
            }
        });
    }

    private async Task LoadMoreTagsAsync(Guid sessionId)
    {
        if (tagEnumerator == null || isLoadingTags || currentPostSessionId != sessionId) return;
        
        isLoadingTags = true;
        try 
        {
            var enumerator = tagEnumerator;
            var loaded = new List<string>();
            
            for (int i = 0; i < 10; i++)
            {
                if (currentPostSessionId != sessionId) return;

                if (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    loaded.Add(enumerator.Current);
                }
                else
                {
                    break;
                }
            }

            if (loaded.Count > 0 && currentPostSessionId == sessionId)
            {
                Application.MainLoop.Invoke(() =>
                {
                    if (currentPostSessionId != sessionId) return;

                    var scrollToIndex = displayedTags.Count;
                    if (displayedTags.Count == 0)
                    {
                        var countText = totalTagCount < 0 ? "..." : totalTagCount.ToString();
                        displayedTags.Add($"--- Showing Tags ({countText} total) ---");
                        scrollToIndex = 0;
                    }
                    
                    displayedTags.AddRange(loaded);
                    SafeSetListViewSource(tagsListView, displayedTags);

                    if (scrollToIndex < tagsListView.Source.Count)
                    {
                        try { tagsListView.TopItem = scrollToIndex; } catch {}
                        try { tagsListView.SelectedItem = scrollToIndex; } catch {}
                    }
                });
            }
        }
        finally
        {
            if (currentPostSessionId == sessionId)
            {
                isLoadingTags = false;
            }
        }
    }

    private async Task LoadMoreCommentsAsync(Guid sessionId)
    {
        if (commentEnumerator == null || isLoadingComments || currentPostSessionId != sessionId) return;
        
        isLoadingComments = true;
        try 
        {
            var enumerator = commentEnumerator;
            var loaded = new List<Comment>();
            
            for (int i = 0; i < 10; i++)
            {
                if (currentPostSessionId != sessionId) return;

                if (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    loaded.Add(enumerator.Current.Value);
                }
                else
                {
                    break;
                }
            }

            if (loaded.Count > 0 && currentPostSessionId == sessionId)
            {
                var formatted = loaded
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => $"[{c.CreatedAt:g}] {c.Author}: {c.Text}")
                    .ToList();

                Application.MainLoop.Invoke(() =>
                {
                    if (currentPostSessionId != sessionId) return;

                    var scrollToIndex = displayedComments.Count;
                    if (displayedComments.Count == 0)
                    {
                        var countText = totalCommentCount < 0 ? "..." : totalCommentCount.ToString();
                        displayedComments.Add($"--- Showing Comments ({countText} total) ---");
                        scrollToIndex = 0;
                    }

                    displayedComments.AddRange(formatted);
                    SafeSetListViewSource(commentListView, displayedComments);

                    if (scrollToIndex < commentListView.Source.Count)
                    {
                        try { commentListView.TopItem = scrollToIndex; } catch {}
                        try { commentListView.SelectedItem = scrollToIndex; } catch {}
                    }
                });
            }
        }
        finally 
        {
            if (currentPostSessionId == sessionId)
            {
                isLoadingComments = false;
            }
        }
    }

    private async Task RefreshTagsAsync(Guid sessionId)
    {
        if (currentPostSessionId != sessionId) return;

        await DisposeTagEnumeratorAsync().ConfigureAwait(false);
        tagEnumerator = documentCollection.GetElementsAsync<string>(selectedBlogPostId, TagsPropertyName).GetAsyncEnumerator();
        
        Application.MainLoop.Invoke(() => {
            if (currentPostSessionId != sessionId) return;
            displayedTags.Clear();
            SafeSetListViewSource(tagsListView, displayedTags);
        });
        
        await LoadMoreTagsAsync(sessionId).ConfigureAwait(false);
    }

    private async Task RefreshCommentsAsync(Guid sessionId)
    {
        if (currentPostSessionId != sessionId) return;

        await DisposeCommentEnumeratorAsync().ConfigureAwait(false);
        commentEnumerator = documentCollection.GetElementsAsync<KeyValuePair<DateTimeOffset, Comment>>(selectedBlogPostId, CommentsPropertyName).GetAsyncEnumerator();
        
        Application.MainLoop.Invoke(() => {
            if (currentPostSessionId != sessionId) return;
            displayedComments.Clear();
            SafeSetListViewSource(commentListView, displayedComments);
        });
        
        await LoadMoreCommentsAsync(sessionId).ConfigureAwait(false);
    }

    private async Task DisposeTagEnumeratorAsync()
    {
        var e = tagEnumerator;
        tagEnumerator = null;
        if (e != null) await e.DisposeAsync().ConfigureAwait(false);
    }

    private async Task DisposeCommentEnumeratorAsync()
    {
        var e = commentEnumerator;
        commentEnumerator = null;
        if (e != null) await e.DisposeAsync().ConfigureAwait(false);
    }

    private async Task DisposeEnumeratorsAsync()
    {
        await DisposeTagEnumeratorAsync().ConfigureAwait(false);
        await DisposeCommentEnumeratorAsync().ConfigureAwait(false);
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

        btnOk.Clicked += () => {
            Application.RequestStop();
            var title = titleText.Text?.ToString();
            var content = contentText.Text?.ToString();
            if (string.IsNullOrWhiteSpace(title)) return;

            Task.Run(async () =>
            {
                try
                {
                    var id = Guid.NewGuid();
                    var newPost = new BlogPost { Id = id, Title = title, Content = content ?? "" };
                    
                    await chunkManager.InitializeAsync(new BlogPost { Id = id }).ConfigureAwait(false);
                    var emptyDoc = await documentCollection.GetDocumentHeaderAsync(id).ConfigureAwait(false);
                    var patcher = currentScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
                    var applicator = currentScope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
                    
                    var patch = patcher.GeneratePatch(emptyDoc.Value, newPost);
                    
                    // Operations generated during patch apply will be captured automatically by the JournalingApplicatorDecorator
                    await applicator.ApplyPatchAsync(emptyDoc.Value, patch).ConfigureAwait(false);

                    Application.MainLoop.Invoke(() => {
                        SaveReplicaStates();
                        lock (blogPostIds)
                        {
                            if (!blogPostIds.Contains(id)) {
                                blogPostIds.Add(id);
                            }
                        }
                        UpdateSyncStatusUI();
                    });
                    
                    await LoadBlogPostHeadersAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Application.MainLoop.Invoke(() => MessageBox.ErrorQuery("Error", ex.Message, "Ok"));
                }
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

        btnOk.Clicked += () => {
            Application.RequestStop();
            var author = authorText.Text?.ToString();
            var content = contentText.Text?.ToString();
            if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(content)) return;

            var sessionId = currentPostSessionId;
            Task.Run(async () =>
            {
                try
                {
                    var comment = new Comment(Guid.NewGuid(), author, content, DateTimeOffset.UtcNow);
                    
                    var headerContent = await documentCollection.GetDocumentHeaderAsync(selectedBlogPostId).ConfigureAwait(false);
                    var patcher = currentScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
                    var applicator = currentScope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
                    
                    var fromDoc = new CrdtDocument<BlogPost>(
                        new BlogPost { Id = selectedBlogPostId, Comments = new Dictionary<DateTimeOffset, Comment>() }, 
                        headerContent.Value.Metadata);
                    
                    var operation = patcher.GenerateOperation(fromDoc, x => x.Comments, new MapSetIntent(comment.CreatedAt, comment));
                    var patch = new CrdtPatch(new[] { operation });
                    
                    // Journal decorator automatically saves
                    await applicator.ApplyPatchAsync(fromDoc, patch).ConfigureAwait(false);

                    var commentCount = await documentCollection.GetElementCountAsync(selectedBlogPostId, CommentsPropertyName).ConfigureAwait(false);
                    
                    Application.MainLoop.Invoke(() => {
                        SaveReplicaStates();
                        totalCommentCount = commentCount;
                        UpdateSyncStatusUI();
                    });
                    
                    await RefreshCommentsAsync(sessionId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Application.MainLoop.Invoke(() => MessageBox.ErrorQuery("Error", ex.Message, "Ok"));
                }
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

        btnOk.Clicked += () => {
            Application.RequestStop();
            var newTag = tagText.Text?.ToString();
            if (string.IsNullOrWhiteSpace(newTag)) return;

            var sessionId = currentPostSessionId;
            Task.Run(async () =>
            {
                try
                {
                    var headerContent = await documentCollection.GetDocumentHeaderAsync(selectedBlogPostId).ConfigureAwait(false);
                    var patcher = currentScope.ServiceProvider.GetRequiredService<ICrdtPatcher>();
                    var applicator = currentScope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();
                    
                    var header = headerContent.Value.Data;
                    var fromDoc = new CrdtDocument<BlogPost>(header, headerContent.Value.Metadata);
                    
                    var operation = patcher.GenerateOperation(fromDoc, x => x.Tags, new AddIntent(newTag));
                    var patch = new CrdtPatch(new[] { operation });
                    
                    // Journal decorator automatically saves
                    await applicator.ApplyPatchAsync(fromDoc, patch).ConfigureAwait(false);

                    var tagCount = await documentCollection.GetElementCountAsync(selectedBlogPostId, TagsPropertyName).ConfigureAwait(false);

                    Application.MainLoop.Invoke(() => {
                        SaveReplicaStates();
                        totalTagCount = tagCount;
                        UpdateSyncStatusUI();
                    });
                    
                    await RefreshTagsAsync(sessionId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Application.MainLoop.Invoke(() => MessageBox.ErrorQuery("Error", ex.Message, "Ok"));
                }
            });
        };
        btnCancel.Clicked += () => Application.RequestStop();

        dialog.AddButton(btnOk);
        dialog.AddButton(btnCancel);
        dialog.Add(tagLabel, tagText);
        Application.Run(dialog);
    }

    private void SyncReplicas()
    {
        Task.Run(async () => 
        {
            try
            {
                var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
                var vvSyncService = serviceProvider.GetRequiredService<IVersionVectorSyncService>();
                int syncCount = 0;

                foreach (var replica in replicaIds.Where(r => r != currentReplicaId))
                {
                    var req = vvSyncService.CalculateRequirement(currentReplicaId, replicaDvvs[currentReplicaId], replica, replicaDvvs[replica]);
                    if (!req.IsBehind) continue;

                    // Fetch missing operations directly from the source replica's journal
                    using var sourceScope = scopeFactory.CreateScope(replica, replicaDvvs[replica]);
                    var sourceJournalManager = sourceScope.ServiceProvider.GetRequiredService<IJournalManager>();
                    var missingOpsStream = sourceJournalManager.GetMissingOperationsAsync(req);

                    var opsByDocument = new Dictionary<string, List<CrdtOperation>>();
                    await foreach (var jOp in missingOpsStream.ConfigureAwait(false))
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
                        var keys = new HashSet<IComparable>(await documentCollection.GetAllLogicalKeysAsync().ConfigureAwait(false));
                        var targetApplicator = currentScope.ServiceProvider.GetRequiredService<IAsyncCrdtApplicator>();

                        foreach (var kvp in opsByDocument)
                        {
                            if (!Guid.TryParse(kvp.Key, out var logicalKey)) continue;

                            if (!keys.Contains(logicalKey))
                            {
                                await chunkManager.InitializeAsync(new BlogPost { Id = logicalKey }).ConfigureAwait(false);
                                keys.Add(logicalKey);
                            }
                            
                            var headerDoc = await documentCollection.GetDocumentHeaderAsync(logicalKey).ConfigureAwait(false);
                            
                            async IAsyncEnumerable<JournaledOperation> GetDocumentOpsStreamAsync()
                            {
                                foreach (var op in kvp.Value)
                                {
                                    yield return new JournaledOperation(kvp.Key, op);
                                }
                                
                                await Task.CompletedTask.ConfigureAwait(false);
                            }

                            // Using ApplyOperationsAsync guarantees that out-of-order operations are retried and dependencies resolved properly
                            await targetApplicator.ApplyOperationsAsync(headerDoc.Value, GetDocumentOpsStreamAsync()).ConfigureAwait(false);
                            syncCount++;
                        }
                    }
                }

                Application.MainLoop.Invoke(() => {
                    SaveReplicaStates();
                    MessageBox.Query("Sync Complete", $"Successfully pulled and applied patches for {syncCount} documents to '{currentReplicaId}'.", "Ok");
                    UpdateSyncStatusUI();
                });
                
                await LoadBlogPostHeadersAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() => MessageBox.ErrorQuery("Error", ex.Message, "Ok"));
            }
        });
    }
}