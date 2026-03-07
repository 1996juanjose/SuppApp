using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services.Interfaces;

namespace SuplementosApp.Web.Services;

public class VentaService : IVentaService
{
    private readonly AppDbContext _context;

    public VentaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Venta>> GetAllAsync(DateTime? desde = null, DateTime? hasta = null, int? vendedorId = null, int? estadoVentaId = null, string? region = null, int? metodoPagoId = null, string? celular = null)
    {
        var query = _context.Ventas
            .Include(v => v.Vendedor)
            .Include(v => v.EstadoVenta)
            .Include(v => v.MetodoEnvio)
            .Include(v => v.MetodoPago)
            .Include(v => v.CanalVenta)
            .AsQueryable();

        if (desde.HasValue)
        {
            query = query.Where(v => v.Fecha >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(v => v.Fecha <= hasta.Value);
        }

        if (vendedorId.HasValue)
        {
            query = query.Where(v => v.VendedorId == vendedorId.Value);
        }

        if (estadoVentaId.HasValue)
        {
            query = query.Where(v => v.EstadoVentaId == estadoVentaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            query = query.Where(v => v.Region != null && v.Region.Contains(region));
        }

        if (metodoPagoId.HasValue)
        {
            query = query.Where(v => v.MetodoPagoId == metodoPagoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(celular))
        {
            query = query.Where(v => v.Celular != null && v.Celular.Contains(celular));
        }

        return await query
            .OrderByDescending(v => v.Fecha)
            .ThenByDescending(v => v.Id)
            .ToListAsync();
    }

    public async Task<Venta?> GetByIdAsync(int id)
    {
        return await _context.Ventas
            .Include(v => v.Vendedor)
            .Include(v => v.EstadoVenta)
            .Include(v => v.MetodoEnvio)
            .Include(v => v.MetodoPago)
            .Include(v => v.CanalVenta)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<Venta> CreateAsync(Venta venta)
    {
        // Normalizar IDs de catálogos: si llegan como 0 desde la UI, usar un valor por defecto (1)
        if (venta.EstadoVentaId == 0)
        {
            venta.EstadoVentaId = 1; // Debe existir un EstadoVenta con Id = 1
        }
        if (venta.MetodoEnvioId == 0)
        {
            venta.MetodoEnvioId = 1; // Debe existir un MetodoEnvio con Id = 1
        }
        if (venta.MetodoPagoId == 0)
        {
            venta.MetodoPagoId = 1; // Debe existir un MetodoPago con Id = 1
        }
        if (venta.CanalVentaId == 0)
        {
            venta.CanalVentaId = 1; // Debe existir un CanalVenta con Id = 1
        }

        _context.Ventas.Add(venta);
        await _context.SaveChangesAsync();
        return venta;
    }

    public async Task<Venta> UpdateAsync(Venta venta)
    {
        var existente = await _context.Ventas
            .FirstOrDefaultAsync(v => v.Id == venta.Id);

        if (existente == null)
        {
            throw new InvalidOperationException($"No se encontró la venta con Id {venta.Id}");
        }

        existente.Fecha = venta.Fecha;
        existente.NombreCliente = venta.NombreCliente;
        existente.Region = venta.Region;
        existente.DireccionEnvio = venta.DireccionEnvio;
        existente.Celular = venta.Celular;
        existente.DNI = venta.DNI;
        existente.EstadoVentaId = venta.EstadoVentaId == 0 ? 1 : venta.EstadoVentaId;
        existente.Observacion = venta.Observacion;
        existente.NroOrden = venta.NroOrden;
        existente.Cod = venta.Cod;
        existente.Clave = venta.Clave;
        existente.CostoEnvio = venta.CostoEnvio;
        existente.Productos = venta.Productos;
        existente.MetodoEnvioId = venta.MetodoEnvioId == 0 ? 1 : venta.MetodoEnvioId;
        existente.MetodoPagoId = venta.MetodoPagoId == 0 ? 1 : venta.MetodoPagoId;
        existente.CanalVentaId = venta.CanalVentaId == 0 ? 1 : venta.CanalVentaId;
        existente.PrecioVenta = venta.PrecioVenta;
        existente.Pagado = venta.Pagado;
        existente.VendedorId = venta.VendedorId;
        existente.Comision = venta.Comision;
        existente.TipoContacto = venta.TipoContacto;

        await _context.SaveChangesAsync();
        return existente;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var venta = await _context.Ventas.FindAsync(id);
        if (venta == null)
        {
            return false;
        }

        _context.Ventas.Remove(venta);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PagarAsync(int id)
    {
        var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.Id == id);
        if (venta == null)
        {
            return false;
        }

        venta.Pagado = venta.PrecioVenta;
        await _context.SaveChangesAsync();
        return true;
    }
}
