using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services.Interfaces;

namespace SuplementosApp.Web.Services;

public class EstadosVentaService : IEstadosVentaService
{
    private readonly AppDbContext _context;

    public EstadosVentaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EstadoVenta>> GetAllAsync()
    {
        return await _context.EstadosVenta
            .OrderBy(e => e.Nombre)
            .ToListAsync();
    }
}
