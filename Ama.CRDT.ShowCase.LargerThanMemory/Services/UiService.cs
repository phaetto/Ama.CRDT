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
    private ListView postListView;
    private ListView commentListView;
    private readonly List<string> displayedComments = new();
    private List<IPartition> commentPartitions = [];
    private int currentPartitionIndex = -1;

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

        postListView = new ListView(blogPostIds.Select(id => $"Blog Post: {id}").ToList())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        postListView.SelectedItemChanged += OnPostSelected;
        leftPane.Add(postListView);

        var rightPane = new FrameView("Comments (Loaded On-Demand)")
        {
            X = 40,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };

        commentListView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        rightPane.Add(commentListView);

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
        currentScope.Dispose();
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
        
        if (postListView?.SelectedItem >= 0)
        {
            OnPostSelected(new ListViewItemEventArgs(postListView.SelectedItem, postListView.Source.ToList()[postListView.SelectedItem]));
        }
        else if (commentListView is not null)
        {
            displayedComments.Clear();
            commentListView.SetSource(displayedComments);
        }
    }
    
    private async void OnPostSelected(ListViewItemEventArgs args)
    {
        if (partitionManager is null || commentListView is null || args is null || args.Item < 0 || blogPostIds is null || args.Item >= blogPostIds.Count)
        {
            if (commentListView is not null)
            {
                displayedComments.Clear();
                commentListView.SetSource(displayedComments);
            }
            return;
        }

        selectedBlogPostId = blogPostIds[args.Item];
        
        commentPartitions = await partitionManager.GetAllDataPartitionsAsync(selectedBlogPostId);
        currentPartitionIndex = -1;
        displayedComments.Clear();
        commentListView.SetSource(displayedComments);
        LoadNextPartition();
    }

    private async void LoadNextPartition()
    {
        if (commentPartitions == null || currentPartitionIndex + 1 >= commentPartitions.Count)
        {
            MessageBox.Query("Info", "No more partitions to load.", "Ok");
            return;
        }

        currentPartitionIndex++;
        var partition = commentPartitions[currentPartitionIndex];
        var content = await partitionManager.GetPartitionContentAsync(partition.GetPartitionKey());

        if (content.HasValue && content.Value.Data?.Comments is { Count: > 0 } comments)
        {
            var newComments = comments
                .Select(c => $"{c.Author}: {c.Text.Substring(0, Math.Min(c.Text.Length, 50))}...")
                .ToList();
            
            displayedComments.Add($"--- Partition {currentPartitionIndex+1}/{commentPartitions.Count} ---");
            displayedComments.AddRange(newComments);

            commentListView.SetSource(displayedComments);
            commentListView.MoveEnd();
        }
    }
}