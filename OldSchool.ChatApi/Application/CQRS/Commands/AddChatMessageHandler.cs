using MediatR;
using OldSchool.ChatApi.Application.CQRS.Commands;
using OldSchool.ChatApi.Application.Dtos;
using OldSchool.ChatApi.Application.Interfaces;
using OldSchool.ChatApi.Domain.Entities;

namespace OldSchool.ChatApi.Application.CQRS.Commands;

public sealed class AddChatMessageHandler(IChatRepository chatRepository) : IRequestHandler<AddChatMessageCommand, AddChatMessageResult>
{
    public async Task<AddChatMessageResult> Handle(AddChatMessageCommand request, CancellationToken cancellationToken)
    {
        ChatMessageDto dto = request.Message;

        var thread = await chatRepository.GetThreadByPhoneAsync(dto.PhoneNumber, cancellationToken)
            ?? await chatRepository.UpsertThreadAsync(new ChatThread
            {
                PhoneNumber = dto.PhoneNumber,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);

        var message = await chatRepository.AddMessageAsync(new ChatMessage
        {
            ChatThreadId = thread.Id,
            PhoneNumber = dto.PhoneNumber,
            MessageText = dto.MessageText,
            Direction = dto.Direction,
            Source = dto.Source,
            SentAt = dto.SentAt,
            ReceivedAt = dto.ReceivedAt,
            ReadAt = dto.ReadAt
        }, cancellationToken);

        thread.LastMessageAt = message.CreatedAt;
        await chatRepository.UpsertThreadAsync(thread, cancellationToken);

        return new AddChatMessageResult(thread.Id, message.Id);
    }
}