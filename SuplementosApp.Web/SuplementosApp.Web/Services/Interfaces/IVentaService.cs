using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SuplementosApp.Web.Data;

namespace SuplementosApp.Web.Services.Interfaces;

public interface IVentaService
{
    Task<IReadOnlyList<Venta>> GetAllAsync(DateTime? desde = null, DateTime? hasta = null, int? vendedorId = null, int? estadoVentaId = null, string? region = null, int? metodoPagoId = null, string? celular = null);
    Task<Venta?> GetByIdAsync(int id);
    Task<Venta> CreateAsync(Venta venta);
    Task<Venta> UpdateAsync(Venta venta);
    Task<bool> DeleteAsync(int id);
    Task<bool> PagarAsync(int id);
}
