using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Pages.Admin.Products;

[Authorize(Roles = "Gerencia")]
public class EditModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(80)]
        [Display(Name = "Nombre")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;

        public List<PriceRow> Prices { get; set; } = new();
    }

    public class PriceRow
    {
        public int? Id { get; set; }

        [Range(1, 999)]
        [Display(Name = "Cantidad")]
        public int Quantity { get; set; }

        [Range(0, 999999)]
        [Display(Name = "Precio (S/)")]
        public decimal Price { get; set; }

        public bool Delete { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            Input = new InputModel { IsActive = true };
            return Page();
        }

        var product = await db.Products
            .AsNoTracking()
            .Include(x => x.Prices.OrderBy(p => p.Quantity))
            .FirstOrDefaultAsync(x => x.Id == id);

        if (product is null) return NotFound();

        Input = new InputModel
        {
            Id = product.Id,
            Name = product.Name,
            IsActive = product.IsActive,
            Prices = product.Prices.Select(p => new PriceRow
            {
                Id = p.Id,
                Quantity = p.Quantity,
                Price = p.Price
            }).ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (Input.Id == 0)
        {
            var product = new Product
            {
                Name = Input.Name.Trim(),
                IsActive = Input.IsActive
            };

            foreach (var row in Input.Prices.Where(p => !p.Delete))
            {
                product.Prices.Add(new ProductPrice { Quantity = row.Quantity, Price = row.Price });
            }

            db.Products.Add(product);
        }
        else
        {
            var product = await db.Products
                .Include(x => x.Prices)
                .FirstOrDefaultAsync(x => x.Id == Input.Id);

            if (product is null) return NotFound();

            product.Name = Input.Name.Trim();
            product.IsActive = Input.IsActive;

            foreach (var row in Input.Prices)
            {
                if (row.Delete)
                {
                    if (row.Id.HasValue)
                    {
                        var existing = product.Prices.FirstOrDefault(p => p.Id == row.Id.Value);
                        if (existing is not null) db.ProductPrices.Remove(existing);
                    }
                }
                else if (row.Id.HasValue)
                {
                    var existing = product.Prices.FirstOrDefault(p => p.Id == row.Id.Value);
                    if (existing is not null)
                    {
                        existing.Quantity = row.Quantity;
                        existing.Price = row.Price;
                    }
                }
                else
                {
                    product.Prices.Add(new ProductPrice { Quantity = row.Quantity, Price = row.Price });
                }
            }
        }

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "Producto guardado correctamente.";
        return RedirectToPage("Index");
    }
}
