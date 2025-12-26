using IDCardAutomation.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SqlClient;

namespace IDCardAutomation.Account
{
    public class ForgotPasswordModel : PageModel
    {

        private readonly string _connectionString;
        private readonly IConfiguration _config;

        public ForgotPasswordModel(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
            _config = config;
        }

        [BindProperty] public string Email { get; set; }

        public IActionResult OnPost()
        {
            var token = Guid.NewGuid().ToString();
            using var con = new SqlConnection(_connectionString);
            var cmd = new SqlCommand("UPDATE Users SET ResetToken = @Token, ResetTokenExpiry = @Expiry WHERE Email = @Email", con);
            cmd.Parameters.AddWithValue("@Token", token);
            cmd.Parameters.AddWithValue("@Expiry", DateTime.Now.AddHours(1));
            cmd.Parameters.AddWithValue("@Email", Email);
            con.Open();
            var updated = cmd.ExecuteNonQuery();

            if (updated > 0)
            {
                // Build full reset link
                var resetLink = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { token = token },
                    protocol: Request.Scheme
                );

                // Send Email
                var emailSender = new EmailSender(_config);
                emailSender.SendResetLink(Email, resetLink);

                TempData["Message"] = "Reset link sent. Check your email.";
                return RedirectToPage("SignIn");
            }

            ModelState.AddModelError("", "Email not found");
            return Page();
        }
    }
}
