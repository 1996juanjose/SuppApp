using System.Collections.Generic;
using System.Threading.Tasks;
using SuplementosApp.Web.Data;

namespace SuplementosApp.Web.Services.Interfaces;

public interface IMetodosPagoService
{
    Task<IReadOnlyList<MetodoPago>> GetAllAsync();
}
