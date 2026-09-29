using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RabbitShovelMigration.Core;

/// <summary>Talks to the RabbitMQ HTTP Management API. One instance serves any broker: credentials travel per request in <see cref="RabbitEndpointConfig"/>.</summary>
public sealed class RabbitManagementClient : IRabbitManagementClient, IDisposable
{
    private readonly HttpClient _http = new();

    public async Task<QueueInfo?> GetQueueAsync(RabbitEndpointConfig endpoint, string queueName, CancellationToken ct = default)
    {
        using var response = await SendAsync(endpoint, HttpMethod.Get, QueueUrl(endpoint, queueName), body: null, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return ParseQueue(json);
    }

    public async Task<IReadOnlyList<string>> ListQueueNamesAsync(RabbitEndpointConfig endpoint, CancellationToken ct = default)
    {
        var url = $"{endpoint.ManagementBaseUrl}/api/queues/{Uri.EscapeDataString(endpoint.VirtualHost)}";
        using var response = await SendAsync(endpoint, HttpMethod.Get, url, body: null, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToList();
    }

    public async Task DeclareQueueAsync(RabbitEndpointConfig endpoint, QueueInfo queue, CancellationToken ct = default)
    {
        var body = new
        {
            durable = queue.Durable,
            auto_delete = queue.AutoDelete,
            arguments = queue.Arguments,
        };

        using var response = await SendAsync(endpoint, HttpMethod.Put, QueueUrl(endpoint, queue.Name), body, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekMessagesAsync(RabbitEndpointConfig endpoint, string queueName, long count, CancellationToken ct = default)
    {
        var body = new
        {
            count,
            ackmode = "ack_requeue_true",
            encoding = "base64",
        };

        using var response = await SendAsync(endpoint, HttpMethod.Post, $"{QueueUrl(endpoint, queueName)}/get", body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.EnumerateArray().Select(item =>
        {
            var payload = item.GetProperty("payload").GetString() ?? string.Empty;
            var encoding = item.GetProperty("payload_encoding").GetString() ?? "base64";
            var properties = item.TryGetProperty("properties", out var props) ? props.Clone() : default;
            return new QueueMessage(payload, encoding, properties);
        }).ToList();
    }

    public async Task<bool> PublishMessageAsync(RabbitEndpointConfig endpoint, string queueName, QueueMessage message, CancellationToken ct = default)
    {
        var url = $"{endpoint.ManagementBaseUrl}/api/exchanges/{Uri.EscapeDataString(endpoint.VirtualHost)}/amq.default/publish";
        object properties = message.Properties.ValueKind == JsonValueKind.Object
            ? message.Properties
            : new Dictionary<string, object?>();
        var body = new
        {
            properties,
            routing_key = queueName,
            payload = message.Payload,
            payload_encoding = message.PayloadEncoding,
        };

        using var response = await SendAsync(endpoint, HttpMethod.Post, url, body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return json.TryGetProperty("routed", out var routed) && routed.GetBoolean();
    }

    public async Task CreateShovelAsync(RabbitEndpointConfig hostingEndpoint, ShovelDefinition definition, CancellationToken ct = default)
    {
        var body = new
        {
            value = new Dictionary<string, object?>
            {
                ["src-uri"] = definition.SourceUri,
                ["src-queue"] = definition.SourceQueue,
                ["dest-uri"] = definition.DestinationUri,
                ["dest-queue"] = definition.DestinationQueue,
                ["src-delete-after"] = definition.SourceDeleteAfter,
                ["ack-mode"] = definition.AckMode,
            },
        };

        using var response = await SendAsync(hostingEndpoint, HttpMethod.Put, ShovelParameterUrl(hostingEndpoint, definition.VirtualHost, definition.Name), body, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ShovelStatus?> GetShovelStatusAsync(RabbitEndpointConfig hostingEndpoint, string virtualHost, string shovelName, CancellationToken ct = default)
    {
        var url = $"{hostingEndpoint.ManagementBaseUrl}/api/shovels/{Uri.EscapeDataString(virtualHost)}";
        using var response = await SendAsync(hostingEndpoint, HttpMethod.Get, url, body: null, ct);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            // rabbitmq_shovel_management isn't running (400/404 on /api/shovels), but the shovel still
            // works: fall back to the core "parameters" API.
            return await GetShovelStatusFromParametersAsync(hostingEndpoint, virtualHost, shovelName, ct);
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        foreach (var item in json.EnumerateArray())
        {
            if (item.GetProperty("name").GetString() != shovelName)
            {
                continue;
            }

            var stateText = item.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : null;
            var reason = item.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : null;
            return new ShovelStatus(shovelName, ParseState(stateText), reason);
        }

        return null;
    }

    /// <summary>Degraded lookup: a queue-length shovel removes its own parameter when done, so absent = finished (null), present = in progress (state unknown).</summary>
    private async Task<ShovelStatus?> GetShovelStatusFromParametersAsync(RabbitEndpointConfig hostingEndpoint, string virtualHost, string shovelName, CancellationToken ct)
    {
        using var response = await SendAsync(hostingEndpoint, HttpMethod.Get, ShovelParameterUrl(hostingEndpoint, virtualHost, shovelName), body: null, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return new ShovelStatus(shovelName, ShovelState.Unknown, Reason: null);
    }

    private async Task<HttpResponseMessage> SendAsync(RabbitEndpointConfig endpoint, HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{endpoint.Username}:{endpoint.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _http.SendAsync(request, ct);
    }

    private static string QueueUrl(RabbitEndpointConfig endpoint, string queueName) =>
        $"{endpoint.ManagementBaseUrl}/api/queues/{Uri.EscapeDataString(endpoint.VirtualHost)}/{Uri.EscapeDataString(queueName)}";

    private static string ShovelParameterUrl(RabbitEndpointConfig hostingEndpoint, string virtualHost, string shovelName) =>
        $"{hostingEndpoint.ManagementBaseUrl}/api/parameters/shovel/{Uri.EscapeDataString(virtualHost)}/{Uri.EscapeDataString(shovelName)}";

    private static QueueInfo ParseQueue(JsonElement json)
    {
        var name = json.GetProperty("name").GetString()!;
        var durable = json.GetProperty("durable").GetBoolean();
        var autoDelete = json.GetProperty("auto_delete").GetBoolean();
        var messagesReady = json.TryGetProperty("messages_ready", out var mr) ? mr.GetInt64() : 0;

        var arguments = json.TryGetProperty("arguments", out var argsElement) && argsElement.ValueKind == JsonValueKind.Object
            ? argsElement.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value))
            : new Dictionary<string, object?>();

        return new QueueInfo(name, durable, autoDelete, arguments, messagesReady);
    }

    private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText(),
    };

    private static ShovelState ParseState(string? state) => state switch
    {
        "starting" => ShovelState.Starting,
        "running" => ShovelState.Running,
        "terminated" => ShovelState.Terminated,
        _ => ShovelState.Unknown,
    };

    public void Dispose() => _http.Dispose();
}
