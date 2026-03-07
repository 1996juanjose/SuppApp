using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services.Interfaces;

namespace SuplementosApp.Web.Services;

public class MetodosPagoService : IMetodosPagoService
{
    private readonly AppDbContext _context;

    public MetodosPagoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MetodoPago>> GetAllAsync()
    {
        return await _context.MetodosPago
            .OrderBy(e => e.Nombre)
            .ToListAsync();
    }
}
