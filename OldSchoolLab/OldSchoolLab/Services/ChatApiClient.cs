using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OldSchoolLab.Services;

public sealed class ChatApiClient(IHttpClientFactory httpClientFactory, IOptions<ApiEndpointsOptions> options)
{
    public const string HttpClientName = "ChatApiGateway";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<string?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(options.Value.AuthServiceBaseUrl.TrimEnd('/') + "/");

            var response = await client.PostAsJsonAsync("api/auth/login", new
            {
                Username = username,
                Password = password
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<AuthLoginResponse>(JsonOptions, cancellationToken);
            return payload?.Token;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<ChatThreadResponse?> GetThreadAsync(string phoneNumber, string token, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"chat/threads/{Uri.EscapeDataString(phoneNumber)}?skip={skip}&take={take}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatThreadResponse>(JsonOptions, cancellationToken);
    }

    public async Task<ChatSendResponse?> SendMessageAsync(ChatMessageRequest request, string token, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("chat/messages", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatSendResponse>(JsonOptions, cancellationToken);
    }

    private sealed class AuthLoginResponse
    {
        public string? Token { get; set; }
    }
}

public sealed class ChatThreadResponse
{
    public ChatThreadDto? Thread { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = [];
}

public sealed class ChatThreadDto
{
    public string Id { get; set; } = string.Empty;
    public string? CustomerRecordId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsClosed { get; set; }
}

public sealed class ChatMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string ChatThreadId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public int Direction { get; set; }
    public string Source { get; set; } = "WhatsAppWeb";
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ChatMessageRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public int Direction { get; set; } = 1;
    public string Source { get; set; } = "OldSchoolLab";
    public DateTime? SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public sealed class ChatSendResponse
{
    public string ThreadId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
}
