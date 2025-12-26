using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using IDCardAutomation.Utils;  // <-- Make sure this namespace is included for EmailSender

namespace IDCardAutomation.Pages
{
    public class ReissueFormModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly EmailSender _emailSender;

        public ReissueFormModel(IConfiguration configuration, EmailSender emailSender)
        {
            _configuration = configuration;
            _emailSender = emailSender;
        }

        [BindProperty] public string RequestType { get; set; }
        [BindProperty] public string Reason { get; set; }

        public string Name { get; set; }
        public string CodeOrRoll { get; set; }
        public string Role { get; set; }

        public IActionResult OnGet()
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            Role = HttpContext.Session.GetString("UserRole");
            string email = HttpContext.Session.GetString("UserEmail");

            if (userId == null || string.IsNullOrEmpty(Role) || string.IsNullOrEmpty(email))
                return RedirectToPage("/Account/SignIn");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                if (Role == "Employee")
                {
                    string query = "SELECT EmpCode, FullName FROM Employees WHERE Email = @Email";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Email", email);
                    var reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        CodeOrRoll = reader["EmpCode"].ToString();
                        Name = reader["FullName"].ToString();
                    }
                }
                else
                {
                    string query = "SELECT RollNumber, FullName FROM Students WHERE Email = @Email";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Email", email);
                    var reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        CodeOrRoll = reader["RollNumber"].ToString();
                        Name = reader["FullName"].ToString();
                    }
                }
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            Role = HttpContext.Session.GetString("UserRole");

            if (userId == null || string.IsNullOrEmpty(Role))
                return RedirectToPage("/Account/SignIn");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Step 1: Get existing count
                string countQuery = "SELECT COUNT(*) FROM ReissueRequests WHERE UserID = @UserID";
                SqlCommand countCmd = new SqlCommand(countQuery, conn);
                countCmd.Parameters.AddWithValue("@UserID", userId.Value);
                int currentCount = (int)countCmd.ExecuteScalar();

                // Step 2: Insert with incremented count
                string insertQuery = @"
            INSERT INTO ReissueRequests (UserID, Role, RequestType, Reason, CreatedAt, RequestCount)
            VALUES (@UserID, @Role, @RequestType, @Reason, GETDATE(), @RequestCount)";
                SqlCommand insertCmd = new SqlCommand(insertQuery, conn);
                insertCmd.Parameters.AddWithValue("@UserID", userId.Value);
                insertCmd.Parameters.AddWithValue("@Role", Role);
                insertCmd.Parameters.AddWithValue("@RequestType", RequestType);
                insertCmd.Parameters.AddWithValue("@Reason", Reason);
                insertCmd.Parameters.AddWithValue("@RequestCount", currentCount + 1);

                insertCmd.ExecuteNonQuery();
            }

            _emailSender.SendNewRequestNotification(Role);

            TempData["Message"] = "Reissue request submitted successfully.";
            return RedirectToPage("/ReissueStatus");
        }

    }
}
