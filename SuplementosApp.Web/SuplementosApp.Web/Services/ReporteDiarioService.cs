using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services.Interfaces;

namespace SuplementosApp.Web.Services;

public class ReporteDiarioService : IReporteDiarioService
{
    private readonly AppDbContext _context;

    public ReporteDiarioService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ReporteDiario>> GetAllAsync(DateTime? desde = null, DateTime? hasta = null)
    {
        var query = _context.ReportesDiarios
            .Include(r => r.Detalles)
            .ThenInclude(d => d.Vendedor)
            .AsQueryable();

        if (desde.HasValue)
        {
            query = query.Where(r => r.Fecha >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(r => r.Fecha <= hasta.Value);
        }

        return await query
            .OrderByDescending(r => r.Fecha)
            .ToListAsync();
    }

    public async Task<ReporteDiario?> GetByIdAsync(int id)
    {
        return await _context.ReportesDiarios
            .Include(r => r.Detalles)
            .ThenInclude(d => d.Vendedor)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<ReporteDiario?> GetByFechaAsync(DateTime fecha)
    {
        var dateOnly = fecha.Date;
        return await _context.ReportesDiarios
            .Include(r => r.Detalles)
            .ThenInclude(d => d.Vendedor)
            .FirstOrDefaultAsync(r => r.Fecha.Date == dateOnly);
    }

    public async Task<ReporteDiario> CreateOrUpdateAsync(ReporteDiario reporte, IEnumerable<ReporteDiarioDetalle> detalles)
    {
        RecalcularMetricas(reporte);

        if (reporte.Id == 0)
        {
            _context.ReportesDiarios.Add(reporte);
        }
        else
        {
            var existente = await _context.ReportesDiarios
                .FirstOrDefaultAsync(r => r.Id == reporte.Id);

            if (existente == null)
                throw new InvalidOperationException($"No se encontró el reporte con Id {reporte.Id}");

            existente.Fecha = reporte.Fecha;
            existente.GastoPublicidadFB = reporte.GastoPublicidadFB;
            existente.TotalMensajes = reporte.TotalMensajes;
            existente.TotalPedidos = reporte.TotalPedidos;
            existente.FacturacionTotal = reporte.FacturacionTotal;
            existente.ROAS = reporte.ROAS;
            existente.AOV = reporte.AOV;
            existente.CPA = reporte.CPA;
            existente.TasaCierre = reporte.TasaCierre;
            existente.CostoPorMensaje = reporte.CostoPorMensaje;
            existente.Observacion = reporte.Observacion;

            reporte = existente;

            var detallesExistentes = _context.ReportesDiariosDetalle
                .Where(d => d.ReporteDiarioId == reporte.Id);
            _context.ReportesDiariosDetalle.RemoveRange(detallesExistentes);
        }

        foreach (var detalle in detalles)
        {
            detalle.Id = 0;
            detalle.ReporteDiario = reporte;
            detalle.ReporteDiarioId = 0;
            if (detalle.Mensajes > 0)
                detalle.TasaCierre = (decimal)detalle.Pedidos / detalle.Mensajes;
            _context.ReportesDiariosDetalle.Add(detalle);
        }

        await _context.SaveChangesAsync();
        return reporte;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var reporte = await _context.ReportesDiarios.FindAsync(id);
        if (reporte == null)
        {
            return false;
        }

        _context.ReportesDiarios.Remove(reporte);
        await _context.SaveChangesAsync();
        return true;
    }

    private static void RecalcularMetricas(ReporteDiario reporte)
    {
        if (reporte.TotalMensajes > 0)
        {
            reporte.CostoPorMensaje = reporte.GastoPublicidadFB / reporte.TotalMensajes;
            reporte.TasaCierre = reporte.TotalPedidos > 0
                ? (decimal)reporte.TotalPedidos / reporte.TotalMensajes
                : 0;
        }
        else
        {
            reporte.CostoPorMensaje = 0;
            reporte.TasaCierre = 0;
        }

        if (reporte.GastoPublicidadFB > 0)
        {
            reporte.ROAS = reporte.FacturacionTotal / reporte.GastoPublicidadFB;
        }
        else
        {
            reporte.ROAS = 0;
        }

        if (reporte.TotalPedidos > 0)
        {
            reporte.AOV = reporte.FacturacionTotal / reporte.TotalPedidos;
            reporte.CPA = reporte.GastoPublicidadFB / reporte.TotalPedidos;
        }
        else
        {
            reporte.AOV = 0;
            reporte.CPA = 0;
        }
    }
}
