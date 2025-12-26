using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SqlClient;
using IDCardAutomation.Utils;

namespace IDCardAutomation.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly string _connectionString;
        public ResetPasswordModel(IConfiguration config)
            => _connectionString = config.GetConnectionString("DefaultConnection");

        [BindProperty] public string NewPassword { get; set; }
        [BindProperty] public string ConfirmPassword { get; set; }
        [BindProperty(SupportsGet = true)] public string Token { get; set; }

        public IActionResult OnPost()
        {
            if (NewPassword != ConfirmPassword)
                return Page();

            using var con = new SqlConnection(_connectionString);
            var cmd = new SqlCommand("UPDATE Users SET PasswordHash = @Hash, ResetToken = NULL, ResetTokenExpiry = NULL WHERE ResetToken = @Token AND ResetTokenExpiry > GETDATE()", con);
            cmd.Parameters.AddWithValue("@Hash", PasswordHasher.HashPassword(NewPassword));
            cmd.Parameters.AddWithValue("@Token", Token);
            con.Open();
            return cmd.ExecuteNonQuery() == 1 ? RedirectToPage("SignIn") : Page();
        }
    }
}
