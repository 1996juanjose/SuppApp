using System.Text.Json.Serialization;

namespace SuplementosApp.Web.Models;

public class WhatsAppWebhookPayload
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("entry")]
    public List<WhatsAppEntry> Entry { get; set; } = new();
}

public class WhatsAppEntry
{
    [JsonPropertyName("changes")]
    public List<WhatsAppChange> Changes { get; set; } = new();
}

public class WhatsAppChange
{
    [JsonPropertyName("value")]
    public WhatsAppValue? Value { get; set; }

    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;
}

public class WhatsAppValue
{
    [JsonPropertyName("contacts")]
    public List<WhatsAppContact> Contacts { get; set; } = new();

    [JsonPropertyName("messages")]
    public List<WhatsAppMessage> Messages { get; set; } = new();
}

public class WhatsAppContact
{
    [JsonPropertyName("profile")]
    public WhatsAppProfile? Profile { get; set; }

    [JsonPropertyName("wa_id")]
    public string WaId { get; set; } = string.Empty;
}

public class WhatsAppProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class WhatsAppMessage
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public WhatsAppText? Text { get; set; }
}

public class WhatsAppText
{
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}
