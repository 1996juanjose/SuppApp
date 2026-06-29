using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using OldSchool.ChatApi.Domain.Enums;

namespace OldSchool.ChatApi.Domain.Entities;

public class ChatMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ChatThreadId { get; set; } = string.Empty;

    [BsonElement("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [BsonElement("messageText")]
    public string MessageText { get; set; } = string.Empty;

    [BsonElement("direction")]
    public MessageDirection Direction { get; set; }

    [BsonElement("source")]
    public string Source { get; set; } = "WhatsAppWeb";

    [BsonElement("sentAt")]
    public DateTime? SentAt { get; set; }

    [BsonElement("receivedAt")]
    public DateTime? ReceivedAt { get; set; }

    [BsonElement("readAt")]
    public DateTime? ReadAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
