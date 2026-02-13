using AutomationManager.Contracts;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationManager.SDK;

public class AutomationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AutomationApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() } // Serialize enums as strings
        };
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
        var result = await response.Content.ReadFromJsonAsync<PagedResult<AgentResponse>>(_jsonOptions);
        return result?.Items ?? Enumerable.Empty<AgentResponse>();
    }

    public async Task<ScriptTemplateResponse> CreateScriptTemplateAsync(ScriptTemplateRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("script-templates", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScriptTemplateResponse>(_jsonOptions) ?? throw new InvalidOperationException();
    }

    public async Task<ScriptTemplateResponse> CreateScriptTemplateAsync(CreateScriptTemplateRequest request)
    {
        var templateRequest = new ScriptTemplateRequest(
            request.Name,
            request.Description,
            request.ScriptText,
            request.Mode,
            request.LoopCount);
            
        return await CreateScriptTemplateAsync(templateRequest);
    }

    public async Task<IEnumerable<ScriptTemplateResponse>> GetScriptTemplatesAsync()
    {
        var response = await _httpClient.GetAsync("script-templates");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ScriptTemplateResponse>>(_jsonOptions);
        return result?.Items ?? Enumerable.Empty<ScriptTemplateResponse>();
    }

    public async Task<ScriptTemplateResponse?> GetScriptTemplateAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"script-templates/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ScriptTemplateResponse>(_jsonOptions);
    }

    public async Task<ScriptTemplateResponse> UpdateScriptTemplateAsync(Guid id, UpdateScriptTemplateRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"script-templates/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScriptTemplateResponse>(_jsonOptions) ?? throw new InvalidOperationException();
    }

    public async Task DeleteScriptTemplateAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"script-templates/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<ScriptExecutionResponse> StartExecutionAsync(StartExecutionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("executions", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScriptExecutionResponse>(_jsonOptions) ?? throw new InvalidOperationException();
    }

    public async Task<IEnumerable<ScriptExecutionResponse>> GetExecutionsAsync()
    {
        var response = await _httpClient.GetAsync("executions");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ScriptExecutionResponse>>(_jsonOptions);
        return result?.Items ?? Enumerable.Empty<ScriptExecutionResponse>();
    }

    public async Task PauseExecutionAsync(Guid executionId)
    {
        var response = await _httpClient.PostAsync($"executions/{executionId}/pause", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelExecutionAsync(Guid executionId)
    {
        var response = await _httpClient.PostAsync($"executions/{executionId}/cancel", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> RunScriptOnAgentAsync(Guid agentId, string scriptText, ExecutionMode mode = ExecutionMode.RunOnce, int? loopCount = null)
    {
        var request = new RunOnAgentRequest(agentId, scriptText, mode, loopCount);
        var response = await _httpClient.PostAsJsonAsync("executions/run-on-agent", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PauseAgentAsync(Guid agentId)
    {
        var response = await _httpClient.PostAsync($"executions/pause-agent/{agentId}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> StopAgentAsync(Guid agentId)
    {
        var response = await _httpClient.PostAsync($"executions/stop-agent/{agentId}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResumeAgentAsync(Guid agentId)
    {
        var response = await _httpClient.PostAsync($"executions/resume-agent/{agentId}", null);
        return response.IsSuccessStatusCode;
    }

    public record PagedResult<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
    public record RunOnAgentRequest(Guid AgentId, string ScriptText, ExecutionMode Mode = ExecutionMode.RunOnce, int? LoopCount = null);
}