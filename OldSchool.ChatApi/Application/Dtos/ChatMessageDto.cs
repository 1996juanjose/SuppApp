using OldSchool.ChatApi.Domain.Enums;

namespace OldSchool.ChatApi.Application.Dtos;

public class ChatMessageDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public MessageDirection Direction { get; set; }
    public string Source { get; set; } = "WhatsAppWeb";
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}