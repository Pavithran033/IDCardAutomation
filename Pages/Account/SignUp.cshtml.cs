using IDCardAutomation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Reflection.Metadata.Ecma335;

namespace IDCardAutomation.Account
{
    public class SignUpModel : PageModel
    {
        private readonly AuthService _authService;

        public SignUpModel(AuthService authService) => _authService = authService;

        [BindProperty] public string Email { get; set; }
        [BindProperty] public string Password { get; set; }
        [BindProperty] public string ConfirmPassword { get; set; }
        [BindProperty] public string Role { get; set; }
      
        public IActionResult OnPost()
        {
            if (Password != ConfirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match");
                return Page();
            }

            if (_authService.Register(Email, Password, Role))
                return RedirectToPage("SignIn");

            ModelState.AddModelError("", "Registration failed.");
            return Page();
        }
    }
}
