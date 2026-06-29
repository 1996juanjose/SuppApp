using MediatR;
using OldSchool.ChatApi.Application.Dtos;

namespace OldSchool.ChatApi.Application.CQRS.Commands;

public sealed record AddChatMessageCommand(ChatMessageDto Message) : IRequest<AddChatMessageResult>;

public sealed record AddChatMessageResult(string ThreadId, string MessageId);