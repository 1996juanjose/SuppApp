using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services.Interfaces;

namespace SuplementosApp.Web.Services;

public class MetodosEnvioService : IMetodosEnvioService
{
    private readonly AppDbContext _context;

    public MetodosEnvioService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MetodoEnvio>> GetAllAsync()
    {
        return await _context.MetodosEnvio
            .OrderBy(e => e.Nombre)
            .ToListAsync();
    }
}
