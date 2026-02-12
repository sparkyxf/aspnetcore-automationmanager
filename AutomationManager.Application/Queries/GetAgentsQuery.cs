using AutomationManager.Application.DTOs;
using MediatR;

namespace AutomationManager.Application.Queries;

public record GetAgentsQuery(
    string? Search,
    int Page = 1,
    int PageSize = 10,
    string? SortBy = "Name",
    bool SortDescending = false
) : IRequest<PagedResult<AgentDto>>;