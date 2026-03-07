using System.Collections.Generic;
using System.Threading.Tasks;
using SuplementosApp.Web.Data;

namespace SuplementosApp.Web.Services.Interfaces;

public interface ICanalesVentaService
{
    Task<IReadOnlyList<CanalVenta>> GetAllAsync();
}
