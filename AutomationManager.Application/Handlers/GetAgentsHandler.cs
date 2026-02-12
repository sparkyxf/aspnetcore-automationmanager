using AutomationManager.Application.DTOs;
using AutomationManager.Application.Interfaces;
using AutomationManager.Application.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutomationManager.Application.Handlers;

public class GetAgentsHandler : IRequestHandler<GetAgentsQuery, PagedResult<AgentDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAgentsHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<AgentDto>> Handle(GetAgentsQuery request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Agents.GetQueryable();

        if (!string.IsNullOrEmpty(request.Search))
        {
            query = query.Where(x => x.Name.Contains(request.Search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = request.SortBy switch
        {
            "Name" => request.SortDescending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
            _ => query.OrderBy(x => x.Id)
        };

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new AgentDto(
                x.Id,
                x.Name,
                x.Status,
                x.LastSeen
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<AgentDto>(items, totalCount, request.Page, request.PageSize);
    }
}