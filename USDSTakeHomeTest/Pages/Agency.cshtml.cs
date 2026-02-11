using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace USDSTakeHomeTest.Pages;

public class AgencyModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int AgencyId { get; set; }

    public IActionResult OnGet()
    {
        if (AgencyId <= 0)
            return RedirectToPage("/Dashboard");

        return Page();
    }
}
