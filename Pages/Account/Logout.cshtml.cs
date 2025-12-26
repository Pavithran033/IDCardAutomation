using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IDCardAutomation.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            HttpContext.Session.Clear(); // clear all session data
            return RedirectToPage("/Account/SignIn"); // redirect to Login page
        }
    }
}
