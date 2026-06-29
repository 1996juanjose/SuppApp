using MongoDB.Driver;
using OldSchool.ChatApi.Application.Interfaces;
using OldSchool.ChatApi.Domain.Entities;

namespace OldSchool.ChatApi.Infrastructure.Mongo;

public class ChatRepository(IMongoClient mongoClient, IConfiguration configuration) : IChatRepository
{
    private readonly IMongoCollection<ChatThread> _threads = GetDatabase(mongoClient, configuration).GetCollection<ChatThread>("ChatThreads");
    private readonly IMongoCollection<ChatMessage> _messages = GetDatabase(mongoClient, configuration).GetCollection<ChatMessage>("ChatMessages");

    public async Task<ChatThread?> GetThreadByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await _threads.Find(x => x.PhoneNumber == phoneNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ChatThread> UpsertThreadAsync(ChatThread thread, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(thread.Id))
        {
            thread.CreatedAt = DateTime.UtcNow;
            thread.UpdatedAt = DateTime.UtcNow;
            await _threads.InsertOneAsync(thread, cancellationToken: cancellationToken);
            return thread;
        }

        thread.UpdatedAt = DateTime.UtcNow;
        await _threads.ReplaceOneAsync(x => x.Id == thread.Id, thread, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        return thread;
    }

    public async Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        message.CreatedAt = DateTime.UtcNow;
        await _messages.InsertOneAsync(message, cancellationToken: cancellationToken);
        return message;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesByThreadAsync(string threadId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        return await _messages.Find(x => x.ChatThreadId == threadId)
            .SortByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    private static IMongoDatabase GetDatabase(IMongoClient client, IConfiguration configuration)
    {
        var databaseName = configuration["Mongo:Database"] ?? "OldSchoolChat";
        return client.GetDatabase(databaseName);
    }
}