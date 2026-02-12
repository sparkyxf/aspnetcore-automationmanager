using AutomationManager.Contracts;
using System.Net.Http.Json;

namespace AutomationManager.SDK;

public class AutomationApiClient
{
    private readonly HttpClient _httpClient;

    public AutomationApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<AgentResponse>> GetAgentsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 10,
        string? sortBy = "Name",
        bool sortDescending = false)
    {
        var query = $"?page={page}&pageSize={pageSize}&sortBy={sortBy}&sortDescending={sortDescending}";
        if (!string.IsNullOrEmpty(search)) query += $"&search={Uri.EscapeDataString(search)}";

        var response = await _httpClient.GetAsync($"agents{query}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<AgentResponse>>();
        return result?.Items ?? Enumerable.Empty<AgentResponse>();
    }

    public async Task<ScriptTemplateResponse> CreateScriptTemplateAsync(ScriptTemplateRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("script-templates", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScriptTemplateResponse>() ?? throw new InvalidOperationException();
    }

    public record PagedResult<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
}