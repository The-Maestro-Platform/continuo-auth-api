using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AuthApi.Infrastructure;

internal static class SecurityApiRuntimeClient {
    public static async Task<string?> TryResolvePlatformSecretAsync(IConfiguration cfg, string name, CancellationToken ct) {
        var baseUrl =
            cfg["SECURITY_API:BASE_URL"] ??
            cfg["SECURITY_API__BASE_URL"];

        if (string.IsNullOrWhiteSpace(baseUrl)) {
            // AppHost should inject SECURITY_API__BASE_URL. As a last resort, assume default local port.
            baseUrl = "http://localhost:5212";
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)) {
            return null;
        }

        var m2mKey = cfg["M2M_API_KEY"] ?? cfg["M2M__API__KEY"];
        if (string.IsNullOrWhiteSpace(m2mKey)) {
            return null;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.Add("X-M2M-API-KEY", m2mKey);

        var uri = new Uri(baseUri, $"/security/runtime/secrets/{Uri.EscapeDataString(name)}?scope=platform");
        using var resp = await http.GetAsync(uri, ct);
        if (!resp.IsSuccessStatusCode) {
            return null;
        }

        var json = await resp.Content.ReadAsStringAsync(ct);
        try {
            var parsed = JsonSerializer.Deserialize<SecuritySecretResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return parsed?.Value;
        }
        catch {
            return null;
        }
    }

    private sealed record SecuritySecretResponse(string Name, string Value);
}

