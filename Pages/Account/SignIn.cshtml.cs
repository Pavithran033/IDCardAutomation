using IDCardAutomation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IDCardAutomation.Account
{
    public class SignInModel : PageModel
    {
        private readonly AuthService _authService;

        public SignInModel(AuthService authService) => _authService = authService;

        [BindProperty] public string Email { get; set; }
        [BindProperty] public string Password { get; set; }

        public IActionResult OnPost()
        {
            var user = _authService.GetUser(Email, Password);
            if (user != null)
            {
                // ✅ Store values in session for later use in navbar/layout
                HttpContext.Session.SetInt32("UserID", user.UserID);
                HttpContext.Session.SetString("UserRole", user.Role);
                HttpContext.Session.SetString("UserEmail", user.Email);
                HttpContext.Session.SetString("UserName", user.DisplayName ?? user.Email);

                // ✅ Redirect based on role
                return user.Role switch
                {
                    "Admin" => RedirectToPage("/Dashboard"),
                    "Osa" => RedirectToPage("/OsaStudentRequests"),
                    "Deliver" => RedirectToPage("/DeliverStatus"),
                    "Hr" => RedirectToPage("/HrDashboard"),
                    "Student" => RedirectToPage("/ReissueOptions"),
                    "Employee" => RedirectToPage("/ReissueOptions"),
                    "DepartmentUser" => RedirectToPage("/Department/ClearanceRequests"),
                    "Library" => RedirectToPage("/Library/LibraryClearanceRequests"),
                    "DTS" => RedirectToPage("/DTS/DTSClearanceRequests"),
                    _ => RedirectToPage("/Index") // fallback
                };
            }

            // ✅ If login failed
            ModelState.AddModelError("", "Invalid credentials");
            HttpContext.Session.Clear();
            return Page();
        }
    }
}
