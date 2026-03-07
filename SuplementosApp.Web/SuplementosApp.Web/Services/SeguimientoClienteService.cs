using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services.Interfaces;

namespace SuplementosApp.Web.Services;

public class SeguimientoClienteService : ISeguimientoClienteService
{
    private readonly AppDbContext _context;

    public SeguimientoClienteService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SeguimientoCliente>> GetAllAsync(bool? soloSinLlamar = null, string? nombre = null)
    {
        var query = _context.SeguimientoClientes.AsQueryable();

        if (soloSinLlamar.HasValue && soloSinLlamar.Value)
            query = query.Where(c => !c.LlamadaRealizada);

        if (!string.IsNullOrWhiteSpace(nombre))
            query = query.Where(c => c.Nombre != null && c.Nombre.Contains(nombre));

        return await query
            .OrderBy(c => c.LlamadaRealizada)
            .ThenBy(c => c.Nombre)
            .ToListAsync();
    }

    public async Task<SeguimientoCliente?> GetByIdAsync(int id)
    {
        return await _context.SeguimientoClientes.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<SeguimientoCliente> CreateAsync(SeguimientoCliente cliente)
    {
        _context.SeguimientoClientes.Add(cliente);
        await _context.SaveChangesAsync();
        return cliente;
    }

    public async Task<SeguimientoCliente> UpdateAsync(SeguimientoCliente cliente)
    {
        var existente = await _context.SeguimientoClientes.FirstOrDefaultAsync(c => c.Id == cliente.Id);
        if (existente == null)
            throw new InvalidOperationException($"No se encontró el cliente con Id {cliente.Id}");

        existente.Nombre = cliente.Nombre;
        existente.Numero = cliente.Numero;
        existente.ProductoInteres = cliente.ProductoInteres;
        existente.LlamadaRealizada = cliente.LlamadaRealizada;
        existente.Rechazado = cliente.Rechazado;
        existente.Observacion = cliente.Observacion;

        await _context.SaveChangesAsync();
        return existente;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var cliente = await _context.SeguimientoClientes.FindAsync(id);
        if (cliente == null) return false;

        _context.SeguimientoClientes.Remove(cliente);
        await _context.SaveChangesAsync();
        return true;
    }
}
