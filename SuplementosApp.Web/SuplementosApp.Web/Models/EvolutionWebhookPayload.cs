using System.Text.Json.Serialization;

namespace SuplementosApp.Web.Models;

public class EvolutionWebhookPayload
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public EvolutionData? Data { get; set; }
}

public class EvolutionData
{
    [JsonPropertyName("key")]
    public EvolutionKey? Key { get; set; }

    [JsonPropertyName("pushName")]
    public string? PushName { get; set; }

    [JsonPropertyName("message")]
    public EvolutionMessage? Message { get; set; }

    [JsonPropertyName("messageTimestamp")]
    public long? MessageTimestamp { get; set; }
}

public class EvolutionKey
{
    [JsonPropertyName("remoteJid")]
    public string RemoteJid { get; set; } = string.Empty;

    [JsonPropertyName("fromMe")]
    public bool FromMe { get; set; }
}

public class EvolutionMessage
{
    [JsonPropertyName("conversation")]
    public string? Conversation { get; set; }

    [JsonPropertyName("extendedTextMessage")]
    public EvolutionExtendedText? ExtendedTextMessage { get; set; }
}

public class EvolutionExtendedText
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
