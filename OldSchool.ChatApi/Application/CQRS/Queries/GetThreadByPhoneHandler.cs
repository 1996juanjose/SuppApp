using MediatR;
using OldSchool.ChatApi.Application.CQRS.Queries;
using OldSchool.ChatApi.Application.Interfaces;

namespace OldSchool.ChatApi.Application.CQRS.Queries;

public sealed class GetThreadByPhoneHandler(IChatRepository chatRepository) : IRequestHandler<GetThreadByPhoneQuery, GetThreadByPhoneResult?>
{
    public async Task<GetThreadByPhoneResult?> Handle(GetThreadByPhoneQuery request, CancellationToken cancellationToken)
    {
        var thread = await chatRepository.GetThreadByPhoneAsync(request.PhoneNumber, cancellationToken);
        if (thread is null)
        {
            return null;
        }

        var messages = await chatRepository.GetMessagesByThreadAsync(thread.Id, request.Skip, request.Take, cancellationToken);
        return new GetThreadByPhoneResult(thread, messages);
    }
}