using AutomationManager.Application.DTOs;
using AutomationManager.Application.Interfaces;
using AutomationManager.Application.Queries;
using AutomationManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutomationManager.Application.Handlers;

public class GetExecutionsHandler : IRequestHandler<GetExecutionsQuery, PagedResult<ScriptExecutionDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetExecutionsHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ScriptExecutionDto>> Handle(GetExecutionsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<ScriptExecution> query = _unitOfWork.Executions.GetQueryable()
            .Include(e => e.Agent)
            .Include(e => e.ScriptTemplate);

        if (request.AgentId.HasValue)
        {
            query = query.Where(x => x.AgentId == request.AgentId);
        }

        if (request.TemplateId.HasValue)
        {
            query = query.Where(x => x.ScriptTemplateId == request.TemplateId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = request.SortBy switch
        {
            "Id" => request.SortDescending ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id),
            _ => query.OrderByDescending(x => x.Id)
        };

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ScriptExecutionDto(
                x.Id,
                x.AgentId,
                x.ScriptTemplateId,
                x.Status,
                x.PreviousCommands,
                x.CurrentCommand,
                x.NextCommands
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ScriptExecutionDto>(items, totalCount, request.Page, request.PageSize);
    }
}