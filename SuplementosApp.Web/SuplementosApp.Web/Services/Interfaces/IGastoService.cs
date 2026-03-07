using System.Collections.Generic;
using System.Threading.Tasks;
using SuplementosApp.Web.Data;

namespace SuplementosApp.Web.Services.Interfaces;

public interface IGastoService
{
    Task<IReadOnlyList<Gasto>> GetAllAsync(DateTime? desde = null, DateTime? hasta = null, int? categoriaId = null);
    Task<Gasto?> GetByIdAsync(int id);
    Task<Gasto> CreateAsync(Gasto gasto);
    Task<Gasto> UpdateAsync(Gasto gasto);
    Task<bool> DeleteAsync(int id);
    Task<IReadOnlyList<CategoriaGasto>> GetCategoriasAsync();
}
