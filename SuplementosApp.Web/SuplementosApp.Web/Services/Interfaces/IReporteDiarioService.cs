using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SuplementosApp.Web.Data;

namespace SuplementosApp.Web.Services.Interfaces;

public interface IReporteDiarioService
{
    Task<IReadOnlyList<ReporteDiario>> GetAllAsync(DateTime? desde = null, DateTime? hasta = null);
    Task<ReporteDiario?> GetByIdAsync(int id);
    Task<ReporteDiario?> GetByFechaAsync(DateTime fecha);
    Task<ReporteDiario> CreateOrUpdateAsync(ReporteDiario reporte, IEnumerable<ReporteDiarioDetalle> detalles);
    Task<bool> DeleteAsync(int id);
}
