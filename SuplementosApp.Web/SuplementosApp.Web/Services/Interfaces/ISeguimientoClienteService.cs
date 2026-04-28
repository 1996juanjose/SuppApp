using System.Collections.Generic;
using System.Threading.Tasks;
using SuplementosApp.Web.Data;

namespace SuplementosApp.Web.Services.Interfaces;

public interface ISeguimientoClienteService
{
    Task<IReadOnlyList<SeguimientoCliente>> GetAllAsync(bool? soloSinLlamar = null, string? nombre = null);
    Task<SeguimientoCliente?> GetByIdAsync(int id);
    Task<bool> ExisteNumeroAsync(string numero);
    Task<SeguimientoCliente> CreateAsync(SeguimientoCliente cliente);
    Task<SeguimientoCliente> UpdateAsync(SeguimientoCliente cliente);
    Task<bool> DeleteAsync(int id);
}
