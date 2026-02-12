using AutomationManager.Application.DTOs;
using AutomationManager.Application.Interfaces;
using AutomationManager.Application.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutomationManager.Application.Handlers;

public class GetScriptTemplatesHandler : IRequestHandler<GetScriptTemplatesQuery, PagedResult<ScriptTemplateDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetScriptTemplatesHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ScriptTemplateDto>> Handle(GetScriptTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.ScriptTemplates.GetQueryable();

        if (!string.IsNullOrEmpty(request.Search))
        {
            query = query.Where(x => x.Name.Contains(request.Search) || x.Description!.Contains(request.Search));
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
            .Select(x => new ScriptTemplateDto(
                x.Id,
                x.Name,
                x.Description,
                x.ScriptText,
                x.Mode,
                x.LoopCount
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ScriptTemplateDto>(items, totalCount, request.Page, request.PageSize);
    }
}