using Microsoft.AspNetCore.Identity;

namespace OldSchoolLab.Models;

public class ApplicationUser : IdentityUser
{
    public int? CompanyId { get; set; }

    public Company? Company { get; set; }
}
