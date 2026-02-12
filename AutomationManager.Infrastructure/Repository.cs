using AutomationManager.Application.Interfaces;

namespace AutomationManager.Infrastructure;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly AutomationDbContext _context;

    public Repository(AutomationDbContext context)
    {
        _context = context;
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await _context.Set<T>().FindAsync(id);
    }

    public IQueryable<T> GetQueryable()
    {
        return _context.Set<T>();
    }

    public async Task AddAsync(T entity)
    {
        await _context.Set<T>().AddAsync(entity);
    }

    public void Update(T entity)
    {
        _context.Set<T>().Update(entity);
    }

    public void Delete(T entity)
    {
        _context.Set<T>().Remove(entity);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}