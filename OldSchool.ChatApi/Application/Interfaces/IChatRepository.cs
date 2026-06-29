using OldSchool.ChatApi.Domain.Entities;

namespace OldSchool.ChatApi.Application.Interfaces;

public interface IChatRepository
{
    Task<ChatThread?> GetThreadByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<ChatThread> UpsertThreadAsync(ChatThread thread, CancellationToken cancellationToken = default);
    Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesByThreadAsync(string threadId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);
}
