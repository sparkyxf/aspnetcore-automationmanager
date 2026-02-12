using AutomationManager.Application.DTOs;
using MediatR;

namespace AutomationManager.Application.Queries;

public record GetExecutionsQuery(
    Guid? AgentId,
    Guid? TemplateId,
    int Page = 1,
    int PageSize = 10,
    string? SortBy = "Id",
    bool SortDescending = true
) : IRequest<PagedResult<ScriptExecutionDto>>;