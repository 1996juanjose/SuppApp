using System.Collections.Generic;
using System.Threading.Tasks;
using SuplementosApp.Web.Data;

namespace SuplementosApp.Web.Services.Interfaces;

public interface IVendedorService
{
    Task<IReadOnlyList<Vendedor>> GetAllAsync(bool soloActivos = false);
    Task<Vendedor?> GetByIdAsync(int id);
    Task<Vendedor> CreateAsync(Vendedor vendedor);
    Task<Vendedor> UpdateAsync(Vendedor vendedor);
    Task<bool> DeleteAsync(int id);
}
