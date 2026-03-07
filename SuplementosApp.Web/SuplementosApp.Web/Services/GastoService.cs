using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services.Interfaces;

namespace SuplementosApp.Web.Services;

public class GastoService : IGastoService
{
    private readonly AppDbContext _context;

    public GastoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Gasto>> GetAllAsync(DateTime? desde = null, DateTime? hasta = null, int? categoriaId = null)
    {
        var query = _context.Gastos
            .Include(g => g.CategoriaGasto)
            .AsQueryable();

        if (desde.HasValue)
            query = query.Where(g => g.Fecha >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(g => g.Fecha <= hasta.Value);

        if (categoriaId.HasValue)
            query = query.Where(g => g.CategoriaGastoId == categoriaId.Value);

        return await query
            .OrderByDescending(g => g.Fecha)
            .ThenByDescending(g => g.Id)
            .ToListAsync();
    }

    public async Task<Gasto?> GetByIdAsync(int id)
    {
        return await _context.Gastos
            .Include(g => g.CategoriaGasto)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<Gasto> CreateAsync(Gasto gasto)
    {
        _context.Gastos.Add(gasto);
        await _context.SaveChangesAsync();
        return gasto;
    }

    public async Task<Gasto> UpdateAsync(Gasto gasto)
    {
        var existente = await _context.Gastos.FirstOrDefaultAsync(g => g.Id == gasto.Id);
        if (existente == null)
            throw new InvalidOperationException($"No se encontró el gasto con Id {gasto.Id}");

        existente.Fecha = gasto.Fecha;
        existente.Detalle = gasto.Detalle;
        existente.CategoriaGastoId = gasto.CategoriaGastoId;
        existente.Monto = gasto.Monto;

        await _context.SaveChangesAsync();
        return existente;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var gasto = await _context.Gastos.FindAsync(id);
        if (gasto == null) return false;

        _context.Gastos.Remove(gasto);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<CategoriaGasto>> GetCategoriasAsync()
    {
        return await _context.CategoriasGasto
            .OrderBy(c => c.Nombre)
            .ToListAsync();
    }
}
