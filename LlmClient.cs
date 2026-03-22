using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LlmRephraser;

public sealed class LlmClient : IDisposable
{
    private readonly HttpClient _http;

    public LlmClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<string> SendAsync(ProfileConfig config, string systemPrompt, string userText, CancellationToken ct)
    {
        return config.Provider switch
        {
            ApiProvider.Anthropic => await SendAnthropicAsync(config, systemPrompt, userText, ct),
            _ => await SendOpenAIAsync(config, systemPrompt, userText, ct)
        };
    }

    private async Task<string> SendOpenAIAsync(ProfileConfig config, string systemPrompt, string userText, CancellationToken ct)
    {
        var requestBody = new
        {
            model = config.ModelName,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userText }
            },
            max_tokens = 2048,
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, config.ApiEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        using var response = await _http.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new LlmException($"HTTP {(int)response.StatusCode} {response.StatusCode}\n\n{responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content?.Trim() ?? throw new LlmException("Empty response from API");
    }

    private async Task<string> SendAnthropicAsync(ProfileConfig config, string systemPrompt, string userText, CancellationToken ct)
    {
        var requestBody = new
        {
            model = config.ModelName,
            max_tokens = 2048,
            system = systemPrompt,
            messages = new object[]
            {
                new { role = "user", content = userText }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, config.ApiEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("x-api-key", config.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await _http.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new LlmException($"HTTP {(int)response.StatusCode} {response.StatusCode}\n\n{responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var contentArray = doc.RootElement.GetProperty("content");
        foreach (var block in contentArray.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() == "text")
            {
                var text = block.GetProperty("text").GetString();
                if (text != null)
                    return text.Trim();
            }
        }

        throw new LlmException("Empty response from API");
    }

    public void Dispose() => _http.Dispose();
}

public sealed class LlmException : Exception
{
    public LlmException(string message) : base(message) { }
}
