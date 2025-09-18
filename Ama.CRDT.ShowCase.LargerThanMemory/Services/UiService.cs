namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Models.Partitioning;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Partitioning;
using Ama.CRDT.ShowCase.LargerThanMemory.Models;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;

public sealed class UiService
{
    private readonly IServiceProvider serviceProvider;
    private readonly List<string> replicaIds;
    private readonly List<Guid> blogPostIds;
    private Guid selectedBlogPostId;

    private IServiceScope currentScope;
    private IPartitionManager<BlogPost> partitionManager;

    private Window topPane;
    private FrameView rightPane;
    private ListView postListView;
    private TextView postContentView;
    private ListView commentListView;

    private readonly List<string> displayedComments = new();
    private readonly List<BlogPostHeader> blogPostHeaders = new();
    private List<IPartition> commentPartitions = [];
    private int currentPartitionIndex = -1;
    
    private sealed record BlogPostHeader(Guid Id, string Title);

    public UiService(IServiceProvider serviceProvider, List<string> replicaIds, List<Guid> blogPostIds)
    {
        this.serviceProvider = serviceProvider;
        this.replicaIds = replicaIds;
        this.blogPostIds = blogPostIds;
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
            Height = Dim.Fill()
        };
        postListView.SelectedItemChanged += OnPostSelected;
        leftPane.Add(postListView);

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

        var commentsLabel = new Label("Comments (Loaded On-Demand):") { X = 0, Y = Pos.Bottom(postContentView) + 1 };
        commentListView = new ListView()
        {
            X = 0,
            Y = Pos.Bottom(commentsLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        rightPane.Add(postContentLabel, postContentView, commentsLabel, commentListView);


        var statusBar = new StatusBar(new[]
        {
            new StatusItem(Key.F2, "~F2~ Load Next Partition", LoadNextPartition),
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
        currentScope?.Dispose();
        var scopeFactory = serviceProvider.GetRequiredService<ICrdtScopeFactory>();
        currentScope = scopeFactory.CreateScope(replicaId);
        partitionManager = currentScope.ServiceProvider.GetRequiredService<IPartitionManager<BlogPost>>();
        
        if (topPane is not null)
        {
            topPane.Title = $"CRDT Showcase - Viewing: {replicaId}";
        }
        
        LoadBlogPostHeadersAsync();
    }
    
    private async void LoadBlogPostHeadersAsync()
    {
        if (partitionManager is null || blogPostIds is null) return;

        Application.MainLoop.Invoke(() =>
        {
            postListView.SetSource(new List<string> { "Loading..." });
            rightPane.Title = "Selected Post";
            postContentView.Text = "";
            displayedComments.Clear();
            commentListView.SetSource(displayedComments);
        });

        blogPostHeaders.Clear();
        foreach (var id in blogPostIds)
        {
            var key = new CompositePartitionKey(id, null);
            var content = await partitionManager.GetPartitionContentAsync(key);
            if (content.HasValue)
            {
                blogPostHeaders.Add(new BlogPostHeader(content.Value.Data.Id, content.Value.Data.Title));
            }
        }

        Application.MainLoop.Invoke(() =>
        {
            postListView.SetSource(blogPostHeaders.Select(h => h.Title).ToList());
        });
    }

    private async void OnPostSelected(ListViewItemEventArgs args)
    {
        if (partitionManager is null || commentListView is null || args.Item < 0 || blogPostHeaders is null || args.Item >= blogPostHeaders.Count)
        {
            rightPane.Title = "Selected Post";
            postContentView.Text = "";
            displayedComments.Clear();
            commentListView.SetSource(displayedComments);
            return;
        }

        var selectedHeader = blogPostHeaders[args.Item];
        selectedBlogPostId = selectedHeader.Id;

        var headerKey = new CompositePartitionKey(selectedBlogPostId, null);
        var headerContent = await partitionManager.GetPartitionContentAsync(headerKey);

        if (headerContent.HasValue)
        {
            var post = headerContent.Value.Data;
            rightPane.Title = post.Title;
            postContentView.Text = post.Content;
        }

        commentPartitions = await partitionManager.GetAllDataPartitionsAsync(selectedBlogPostId);
        currentPartitionIndex = commentPartitions.Count;
        displayedComments.Clear();
        commentListView.SetSource(displayedComments);
        LoadNextPartition();
    }

    private async void LoadNextPartition()
    {
        if (commentPartitions == null || currentPartitionIndex - 1 < 0)
        {
            MessageBox.Query("Info", "No more partitions to load.", "Ok");
            return;
        }

        currentPartitionIndex--;
        var partition = commentPartitions[currentPartitionIndex];
        var content = await partitionManager.GetPartitionContentAsync(partition.GetPartitionKey());

        if (content.HasValue && content.Value.Data?.Comments is { Count: > 0 } comments)
        {
            var scrollToIndex = displayedComments.Count;
            var newComments = comments.Values
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => $"[{c.CreatedAt:g}] {c.Author}: {c.Text}")
                .ToList();
            
            displayedComments.Add($"--- Partition {currentPartitionIndex + 1}/{commentPartitions.Count} ({comments.Count} comments) ---");
            displayedComments.AddRange(newComments);

            commentListView.SetSource(displayedComments);

            if (scrollToIndex < commentListView.Source.Count)
            {
                commentListView.TopItem = scrollToIndex;
                commentListView.SelectedItem = scrollToIndex;
            }
        }
    }
}