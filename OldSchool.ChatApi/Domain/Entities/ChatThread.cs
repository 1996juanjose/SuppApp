using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OldSchool.ChatApi.Domain.Entities;

public class ChatThread
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? CustomerRecordId { get; set; }

    [BsonElement("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [BsonElement("lastMessageAt")]
    public DateTime? LastMessageAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("isClosed")]
    public bool IsClosed { get; set; }
}