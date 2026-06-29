using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;

namespace OldSchoolApi.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController(ApiDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] int? companyId, CancellationToken cancellationToken)
    {
        var query = db.Products
            .AsNoTracking()
            .Include(x => x.Prices)
            .AsQueryable();

        if (companyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == companyId.Value);
        }

        var products = await query
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.CompanyId,
                x.Name,
                x.PurchaseUnitCost,
                x.IsActive,
                Prices = x.Prices
                    .OrderBy(p => p.Quantity)
                    .Select(p => new { p.Id, p.Quantity, p.Price })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(products);
    }
}