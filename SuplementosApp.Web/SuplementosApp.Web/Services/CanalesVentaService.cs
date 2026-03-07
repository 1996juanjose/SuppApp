using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services.Interfaces;

namespace SuplementosApp.Web.Services;

public class CanalesVentaService : ICanalesVentaService
{
    private readonly AppDbContext _context;

    public CanalesVentaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CanalVenta>> GetAllAsync()
    {
        return await _context.CanalesVenta
            .OrderBy(e => e.Nombre)
            .ToListAsync();
    }
}
