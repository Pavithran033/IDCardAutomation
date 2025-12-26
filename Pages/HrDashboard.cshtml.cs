using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IDCardAutomation.Pages
{
    public class HrDashboardModel : PageModel
    {
        public IActionResult OnGet()

        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Hr")
            {
                return RedirectToPage("/Account/SignIn");
            }

            return Page();
        }
    }
}
