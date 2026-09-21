using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides browser-targeted inspection and control through a local Chromium DevTools endpoint.
/// The endpoint is deliberately restricted to loopback so a tool call cannot target a remote host.
/// </summary>
internal static class BrowserService
{
    private const int DefaultTimeoutMilliseconds = 10_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public static string ListTabs(int debugPort)
    {
        var tabs = GetTabs(debugPort);
        return JsonSerializer.Serialize(new
        {
            debugPort,
            tabs,
        }, JsonOptions);
    }

    public static string WaitForNavigation(
        string? targetId,
        string? urlContains,
        string? titleContains,
        int debugPort,
        int timeoutMilliseconds,
        int pollMilliseconds)
    {
        ValidateWait(timeoutMilliseconds, pollMilliseconds);
        var deadline = Stopwatch.GetTimestamp() + (long)(timeoutMilliseconds / 1000d * Stopwatch.Frequency);

        while (true)
        {
            var match = GetTabs(debugPort).FirstOrDefault(tab => Matches(tab, targetId, urlContains, titleContains));
            if (match is not null)
            {
                return JsonSerializer.Serialize(new
                {
                    matched = true,
                    timedOut = false,
                    debugPort,
                    target = match,
                }, JsonOptions);
            }

            if (Stopwatch.GetTimestamp() >= deadline)
            {
                return JsonSerializer.Serialize(new
                {
                    matched = false,
                    timedOut = true,
                    debugPort,
                    targetId,
                    urlContains,
                    titleContains,
                }, JsonOptions);
            }

            Thread.Sleep(pollMilliseconds);
        }
    }

    public static string InspectAccessibility(string targetId, int debugPort, int maxNodes)
    {
        if (maxNodes is < 1 or > 2_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maxNodes), "maxNodes must be between 1 and 2000.");
        }

        var target = FindPage(debugPort, targetId);
        var result = GetAccessibilityTree(target, debugPort);
        var nodes = result.TryGetProperty("nodes", out var rawNodes) && rawNodes.ValueKind == JsonValueKind.Array
            ? rawNodes.EnumerateArray().Take(maxNodes).Select(MapAccessibilityNode).ToArray()
            : [];
        var totalNodes = rawNodes.ValueKind == JsonValueKind.Array ? rawNodes.GetArrayLength() : 0;

        return JsonSerializer.Serialize(new
        {
            debugPort,
            targetId = target.Id,
            targetTitle = target.Title,
            targetUrl = target.Url,
            truncated = totalNodes > nodes.Length,
            totalNodes,
            nodes,
        }, JsonOptions);
    }

    public static string ClickElement(string targetId, string elementId, int debugPort)
    {
        var target = FindPage(debugPort, targetId);
        var backendNodeId = FindBackendNodeId(target, elementId, debugPort);
        var objectId = ResolveObjectId(target, backendNodeId, debugPort);
        CallFunction(target, objectId, "function () { this.click(); return true; }", null, debugPort);

        return JsonSerializer.Serialize(new
        {
            debugPort,
            targetId = target.Id,
            elementId,
            backendDomNodeId = backendNodeId,
            changed = true,
        }, JsonOptions);
    }

    public static string SetValue(string targetId, string elementId, string value, int debugPort)
    {
        var target = FindPage(debugPort, targetId);
        var backendNodeId = FindBackendNodeId(target, elementId, debugPort);
        var objectId = ResolveObjectId(target, backendNodeId, debugPort);
        CallFunction(
            target,
            objectId,
            "function (nextValue) { " +
            "if ('value' in this) { this.value = nextValue; } " +
            "else if (this.isContentEditable) { this.textContent = nextValue; } " +
            "else { throw new Error('Element does not expose a writable value.'); } " +
            "this.dispatchEvent(new Event('input', { bubbles: true })); " +
            "this.dispatchEvent(new Event('change', { bubbles: true })); " +
            "return true; }",
            value,
            debugPort);

        return JsonSerializer.Serialize(new
        {
            debugPort,
            targetId = target.Id,
            elementId,
            valueLength = value.Length,
            backendDomNodeId = backendNodeId,
            changed = true,
        }, JsonOptions);
    }

    public static string OpenDevTools(string targetId, int debugPort, bool open)
    {
        var target = FindPage(debugPort, targetId);
        var devToolsUrl = string.IsNullOrWhiteSpace(target.DevToolsFrontendUrl)
            ? $"http://127.0.0.1:{debugPort}/devtools/inspector.html?ws=127.0.0.1:{debugPort}/devtools/page/{target.Id}"
            : target.DevToolsFrontendUrl;

        if (open)
        {
            Process.Start(new ProcessStartInfo(devToolsUrl) { UseShellExecute = true });
        }

        return JsonSerializer.Serialize(new
        {
            debugPort,
            targetId = target.Id,
            devToolsUrl,
            opened = open,
        }, JsonOptions);
    }

    private static IReadOnlyList<BrowserTab> GetTabs(int debugPort)
    {
        var endpoint = GetHttpEndpoint(debugPort, "/json/list");
        string payload;
        try
        {
            payload = HttpClient.GetStringAsync(endpoint).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"Could not reach the local browser DevTools endpoint at {endpoint}. Start the browser with --remote-debugging-port={debugPort}.",
                exception);
        }

        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The browser DevTools /json/list endpoint did not return an array.");
        }

        return document.RootElement.EnumerateArray()
            .Select(ReadTab)
            .Where(tab => tab is not null)
            .Cast<BrowserTab>()
            .ToArray();
    }

    private static BrowserTab FindPage(int debugPort, string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("A browser target id is required.", nameof(targetId));
        }

        var target = GetTabs(debugPort).FirstOrDefault(tab =>
            string.Equals(tab.Id, targetId.Trim(), StringComparison.Ordinal));
        if (target is null)
        {
            throw new InvalidOperationException($"Browser target was not found: {targetId}");
        }

        if (!string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Browser target {target.Id} is not a page target (type: {target.Type}).");
        }

        if (string.IsNullOrWhiteSpace(target.WebSocketDebuggerUrl))
        {
            throw new InvalidOperationException($"Browser target {target.Id} does not expose a WebSocket debugger URL.");
        }

        return target;
    }

    private static bool Matches(BrowserTab tab, string? targetId, string? urlContains, string? titleContains)
        => (string.IsNullOrWhiteSpace(targetId) || string.Equals(tab.Id, targetId.Trim(), StringComparison.Ordinal))
            && (string.IsNullOrWhiteSpace(urlContains) || tab.Url.Contains(urlContains.Trim(), StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(titleContains) || tab.Title.Contains(titleContains.Trim(), StringComparison.OrdinalIgnoreCase));

    private static BrowserTab? ReadTab(JsonElement element)
    {
        var id = ReadString(element, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return new BrowserTab(
            id,
            ReadString(element, "type") ?? string.Empty,
            ReadString(element, "title") ?? string.Empty,
            ReadString(element, "url") ?? string.Empty,
            ReadString(element, "webSocketDebuggerUrl"),
            ReadString(element, "devtoolsFrontendUrl"));
    }

    private static JsonElement GetAccessibilityTree(BrowserTab target, int debugPort)
        => CdpClient.Send(
            target.WebSocketDebuggerUrl!,
            "Accessibility.getFullAXTree",
            new { depth = 100 },
            DefaultTimeoutMilliseconds,
            debugPort);

    private static long FindBackendNodeId(BrowserTab target, string elementId, int debugPort)
    {
        if (string.IsNullOrWhiteSpace(elementId) || !elementId.StartsWith("ax:", StringComparison.Ordinal))
        {
            throw new ArgumentException("element_id must be an ax:<nodeId> returned by inspect_browser_accessibility.", nameof(elementId));
        }

        var expectedId = elementId.Trim()[3..];
        var result = GetAccessibilityTree(target, debugPort);
        if (!result.TryGetProperty("nodes", out var rawNodes) || rawNodes.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The browser did not return an accessibility tree.");
        }

        foreach (var node in rawNodes.EnumerateArray())
        {
            if (!string.Equals(ReadNodeId(node), expectedId, StringComparison.Ordinal))
            {
                continue;
            }

            if (node.TryGetProperty("ignored", out var ignored) && ignored.ValueKind == JsonValueKind.True)
            {
                throw new InvalidOperationException($"Accessibility element {elementId} is ignored and cannot be controlled.");
            }

            if (node.TryGetProperty("backendDOMNodeId", out var backend) && backend.TryGetInt64(out var backendNodeId))
            {
                return backendNodeId;
            }

            throw new InvalidOperationException($"Accessibility element {elementId} does not map to a DOM node.");
        }

        throw new InvalidOperationException($"Accessibility element was not found or is stale: {elementId}");
    }

    private static string ResolveObjectId(BrowserTab target, long backendNodeId, int debugPort)
    {
        var result = CdpClient.Send(
            target.WebSocketDebuggerUrl!,
            "DOM.resolveNode",
            new { backendNodeId },
            DefaultTimeoutMilliseconds,
            debugPort);
        if (!result.TryGetProperty("object", out var objectValue)
            || !objectValue.TryGetProperty("objectId", out var objectId)
            || objectId.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(objectId.GetString()))
        {
            throw new InvalidOperationException($"The browser could not resolve backend DOM node {backendNodeId}.");
        }

        return objectId.GetString()!;
    }

    private static void CallFunction(
        BrowserTab target,
        string objectId,
        string functionDeclaration,
        string? value,
        int debugPort)
    {
        object parameters = value is null
            ? new
            {
                objectId,
                functionDeclaration,
                returnByValue = true,
            }
            : new
            {
                objectId,
                functionDeclaration,
                returnByValue = true,
                arguments = new[] { new { value } },
            };
        var result = CdpClient.Send(
            target.WebSocketDebuggerUrl!,
            "Runtime.callFunctionOn",
            parameters,
            DefaultTimeoutMilliseconds,
            debugPort);
        if (result.TryGetProperty("exceptionDetails", out var exceptionDetails))
        {
            throw new InvalidOperationException($"The browser element action failed: {exceptionDetails.GetRawText()}");
        }
    }

    private static SemanticBrowserElement MapAccessibilityNode(JsonElement node)
    {
        var properties = node.TryGetProperty("properties", out var rawProperties)
            && rawProperties.ValueKind == JsonValueKind.Array
            ? rawProperties.EnumerateArray()
                .Select(property => new
                {
                    name = ReadString(property, "name"),
                    value = ReadValue(property, "value"),
                })
                .ToArray()
            : [];
        var childIds = node.TryGetProperty("childIds", out var rawChildren)
            && rawChildren.ValueKind == JsonValueKind.Array
            ? rawChildren.EnumerateArray().Select(ReadScalar).Where(value => value is not null).Cast<string>().ToArray()
            : [];

        return new SemanticBrowserElement(
            "ax:" + ReadNodeId(node),
            ReadValue(node, "role"),
            ReadValue(node, "name"),
            ReadValue(node, "value"),
            ReadValue(node, "description"),
            node.TryGetProperty("ignored", out var ignored) && ignored.ValueKind == JsonValueKind.True,
            node.TryGetProperty("backendDOMNodeId", out var backend) && backend.TryGetInt64(out var backendNodeId)
                ? backendNodeId
                : null,
            childIds,
            properties);
    }

    private static string ReadNodeId(JsonElement node)
        => node.TryGetProperty("nodeId", out var nodeId) ? ReadScalar(nodeId) ?? string.Empty : string.Empty;

    private static string? ReadValue(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Object && property.TryGetProperty("value", out var nestedValue))
        {
            return ReadScalar(nestedValue);
        }

        return ReadScalar(property);
    }

    private static string? ReadString(JsonElement parent, string propertyName)
        => parent.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadScalar(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => value.GetRawText(),
        };

    private static string GetHttpEndpoint(int debugPort, string path)
    {
        ValidatePort(debugPort);
        return $"http://127.0.0.1:{debugPort}{path}";
    }

    private static void ValidatePort(int debugPort)
    {
        if (debugPort is < 1024 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(nameof(debugPort), "debug_port must be between 1024 and 65535.");
        }
    }

    private static void ValidateWait(int timeoutMilliseconds, int pollMilliseconds)
    {
        if (timeoutMilliseconds is < 0 or > 30_000)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "timeout_ms must be between 0 and 30000.");
        }

        if (pollMilliseconds is < 25 or > 1_000)
        {
            throw new ArgumentOutOfRangeException(nameof(pollMilliseconds), "poll_ms must be between 25 and 1000.");
        }
    }

    private sealed record BrowserTab(
        string Id,
        string Type,
        string Title,
        string Url,
        string? WebSocketDebuggerUrl,
        string? DevToolsFrontendUrl);
}

internal sealed record SemanticBrowserElement(
    string Id,
    string? Role,
    string? Name,
    string? Value,
    string? Description,
    bool Ignored,
    long? BackendDomNodeId,
    IReadOnlyList<string> ChildIds,
    IReadOnlyList<object> Properties);

internal static class CdpClient
{
    private static int nextMessageId;

    public static JsonElement Send(
        string webSocketUrl,
        string method,
        object? parameters,
        int timeoutMilliseconds,
        int debugPort)
    {
        ValidateWebSocketUrl(webSocketUrl, debugPort);
        using var socket = new ClientWebSocket();
        using var cancellation = new CancellationTokenSource(timeoutMilliseconds);
        socket.ConnectAsync(new Uri(webSocketUrl), cancellation.Token).GetAwaiter().GetResult();

        var messageId = Interlocked.Increment(ref nextMessageId);
        var request = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = messageId,
            method,
            @params = parameters,
        });
        socket.SendAsync(new ArraySegment<byte>(request), WebSocketMessageType.Text, true, cancellation.Token)
            .GetAwaiter()
            .GetResult();

        while (true)
        {
            var responseText = ReceiveText(socket, cancellation.Token);
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var responseId)
                || responseId.ValueKind != JsonValueKind.Number
                || responseId.GetInt32() != messageId)
            {
                continue;
            }

            if (root.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException($"CDP method {method} failed: {error.GetRawText()}");
            }

            if (!root.TryGetProperty("result", out var result))
            {
                throw new InvalidOperationException($"CDP method {method} returned no result.");
            }

            return result.Clone();
        }
    }

    private static string ReceiveText(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        using var content = new MemoryStream();
        var buffer = new byte[8_192];
        WebSocketReceiveResult received;
        do
        {
            received = socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                .GetAwaiter()
                .GetResult();
            if (received.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("The browser DevTools WebSocket closed before returning a response.");
            }

            content.Write(buffer, 0, received.Count);
        }
        while (!received.EndOfMessage);

        return Encoding.UTF8.GetString(content.ToArray());
    }

    private static void ValidateWebSocketUrl(string webSocketUrl, int debugPort)
    {
        if (!Uri.TryCreate(webSocketUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("ws" or "wss")
            || uri.Port != debugPort
            || !IsLoopback(uri.Host))
        {
            throw new InvalidOperationException("The browser returned a non-loopback DevTools WebSocket URL; refusing to connect.");
        }
    }

    private static bool IsLoopback(string host)
        => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
}
