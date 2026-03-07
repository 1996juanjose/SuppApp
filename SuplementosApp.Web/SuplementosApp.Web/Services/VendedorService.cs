using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services.Interfaces;

namespace SuplementosApp.Web.Services;

public class VendedorService : IVendedorService
{
    private readonly AppDbContext _context;

    public VendedorService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Vendedor>> GetAllAsync(bool soloActivos = false)
    {
        var query = _context.Vendedores.AsQueryable();
        if (soloActivos)
        {
            query = query.Where(v => v.Activo);
        }

        return await query
            .OrderBy(v => v.Nombre)
            .ToListAsync();
    }

    public Task<Vendedor?> GetByIdAsync(int id)
    {
        return _context.Vendedores.FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<Vendedor> CreateAsync(Vendedor vendedor)
    {
        _context.Vendedores.Add(vendedor);
        await _context.SaveChangesAsync();
        return vendedor;
    }

    public async Task<Vendedor> UpdateAsync(Vendedor vendedor)
    {
        // Cargar la entidad existente que ya está siendo trackeada por el DbContext
        var existente = await _context.Vendedores
            .FirstOrDefaultAsync(v => v.Id == vendedor.Id);

        if (existente == null)
        {
            throw new InvalidOperationException($"No se encontró el vendedor con Id {vendedor.Id}");
        }

        // Copiar solo las propiedades editables para evitar conflictos de tracking
        existente.Nombre = vendedor.Nombre;
        existente.Email = vendedor.Email;
        existente.ComisionBase = vendedor.ComisionBase;
        existente.FechaIngreso = vendedor.FechaIngreso;
        existente.Activo = vendedor.Activo;
        existente.Observacion = vendedor.Observacion;

        await _context.SaveChangesAsync();
        return existente;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var vendedor = await _context.Vendedores.FindAsync(id);
        if (vendedor == null)
        {
            return false;
        }

        _context.Vendedores.Remove(vendedor);
        await _context.SaveChangesAsync();
        return true;
    }
}
