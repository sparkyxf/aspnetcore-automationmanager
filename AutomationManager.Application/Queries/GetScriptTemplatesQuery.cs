using AutomationManager.Application.DTOs;
using MediatR;

namespace AutomationManager.Application.Queries;

public record GetScriptTemplatesQuery(
    string? Search,
    int Page = 1,
    int PageSize = 10,
    string? SortBy = "Name",
    bool SortDescending = false
) : IRequest<PagedResult<ScriptTemplateDto>>;

public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);