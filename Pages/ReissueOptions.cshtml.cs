using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IDCardAutomation.Pages
{
    public class ReissueOptionsModel : PageModel
    {
        public string UserRole { get; set; }

        public IActionResult OnGet()
        {
            UserRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(UserRole) || (UserRole != "Student" && UserRole != "Employee"))
            {
                return RedirectToPage("/Account/SignIn");
            }

            return Page();
        }
    }
}
