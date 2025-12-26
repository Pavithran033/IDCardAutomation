using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IDCardAutomation.Pages
{
    public class DashboardModel : PageModel
    {
        public IActionResult OnGet()

        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin")
            {
                return RedirectToPage("/SignIn");
            }

            return Page();
        }
    }
}
