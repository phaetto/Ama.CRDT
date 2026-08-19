namespace Ama.CRDT.ShowCase.LargerThanMemory.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models.LargerThanMemory;
using Ama.CRDT.Services;
using Ama.CRDT.Services.LargerThanMemory;
using Ama.CRDT.ShowCase.LargerThanMemory.Models;
using Microsoft.Data.Sqlite;

/// <summary>
/// Subscribes to CRDT document changes dynamically intercepting chunk applications and generating a high-speed SQLite projection Read Model.
/// Demonstrates that the UI doesn't have to query the CRDT chunks directly at runtime.
/// </summary>
public sealed class BlogPostSqliteProjector : IVirtualDocumentProjector<BlogPost>
{
    private readonly string connectionString;

    public BlogPostSqliteProjector(ReplicaContext replicaContext)
    {
        connectionString = $"Data Source=projections_{replicaContext.ReplicaId}.db";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS BlogPosts (Id TEXT PRIMARY KEY, Title TEXT, Content TEXT);
            CREATE TABLE IF NOT EXISTS Comments (PostId TEXT, CreatedAt TEXT, Author TEXT, Text TEXT, PRIMARY KEY(PostId, CreatedAt));
            CREATE TABLE IF NOT EXISTS Tags (PostId TEXT, Tag TEXT, PRIMARY KEY(PostId, Tag));
        ";
        cmd.ExecuteNonQuery();
    }

    public async Task ProjectHeaderAsync(IComparable logicalKey, BlogPost header, CancellationToken cancellationToken = default)
    {
        using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO BlogPosts (Id, Title, Content) 
            VALUES (@Id, @Title, @Content)
            ON CONFLICT(Id) DO UPDATE SET Title=@Title, Content=@Content;";
            
        cmd.Parameters.AddWithValue("@Id", logicalKey.ToString());
        cmd.Parameters.AddWithValue("@Title", header.Title ?? "");
        cmd.Parameters.AddWithValue("@Content", header.Content ?? "");
        
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ProjectChunkAsync(IComparable logicalKey, string propertyName, IChunk chunk, BlogPost chunkData, CancellationToken cancellationToken = default)
    {
        using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (propertyName == nameof(BlogPost.Comments) && chunkData.Comments != null)
        {
            // For a showcase, we're keeping it simple and using UPSERT. 
            // In a production system, you'd track chunk boundaries to execute full chunk replacement (e.g. DELETE then INSERT).
            foreach (var kv in chunkData.Comments)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Comments (PostId, CreatedAt, Author, Text) 
                    VALUES (@PostId, @CreatedAt, @Author, @Text)
                    ON CONFLICT(PostId, CreatedAt) DO UPDATE SET Author=@Author, Text=@Text;";
                    
                cmd.Parameters.AddWithValue("@PostId", logicalKey.ToString());
                cmd.Parameters.AddWithValue("@CreatedAt", kv.Key.ToString("O"));
                cmd.Parameters.AddWithValue("@Author", kv.Value.Author);
                cmd.Parameters.AddWithValue("@Text", kv.Value.Text);
                
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        else if (propertyName == nameof(BlogPost.Tags) && chunkData.Tags != null)
        {
            foreach (var tag in chunkData.Tags)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Tags (PostId, Tag) 
                    VALUES (@PostId, @Tag)
                    ON CONFLICT(PostId, Tag) DO NOTHING;";
                    
                cmd.Parameters.AddWithValue("@PostId", logicalKey.ToString());
                cmd.Parameters.AddWithValue("@Tag", tag);
                
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task ProjectItemUpsertAsync(IComparable logicalKey, string propertyName, IComparable itemKey, object item, CancellationToken cancellationToken = default)
    {
        // Not used when chunking is enabled. (Invoked natively by IKvDocumentManager instead)
        return Task.CompletedTask;
    }

    public Task ProjectItemDeleteAsync(IComparable logicalKey, string propertyName, IComparable itemKey, CancellationToken cancellationToken = default)
    {
        // Not used when chunking is enabled. (Invoked natively by IKvDocumentManager instead)
        return Task.CompletedTask;
    }
}