using MediatR;
using OldSchool.ChatApi.Domain.Entities;

namespace OldSchool.ChatApi.Application.CQRS.Queries;

public sealed record GetThreadByPhoneQuery(string PhoneNumber, int Skip = 0, int Take = 50) : IRequest<GetThreadByPhoneResult?>;

public sealed record GetThreadByPhoneResult(ChatThread Thread, IReadOnlyList<ChatMessage> Messages);