using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OldSchool.ChatApi.Application.CQRS.Commands;
using OldSchool.ChatApi.Application.CQRS.Queries;
using OldSchool.ChatApi.Application.Dtos;

namespace OldSchool.ChatApi.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public class ChatController(ISender sender) : ControllerBase
{
    [HttpPost("messages")]
    public async Task<IActionResult> AddMessage([FromBody] ChatMessageDto dto, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddChatMessageCommand(dto), cancellationToken);
        return Ok(result);
    }

    [HttpGet("threads/{phoneNumber}")]
    public async Task<IActionResult> GetThreadByPhone(string phoneNumber, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetThreadByPhoneQuery(phoneNumber, skip, take), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}