using IDCardAutomation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace IDCardAutomation.Pages
{
    public class ReissueStatusModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ReissueStatusModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<ReissueRequestView> Requests { get; set; } = new();
        public string Role { get; set; }
        public int EntityID { get; set; } // StudentID or EmployeeID depending on role

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

                // 1. Get EntityID (StudentID or EmployeeID) based on email and role
                string idQuery = Role == "Student"
                    ? "SELECT StudentID FROM Students WHERE Email = @Email"
                    : "SELECT EmployeeID FROM Employees WHERE Email = @Email";

                using (SqlCommand idCmd = new SqlCommand(idQuery, conn))
                {
                    idCmd.Parameters.AddWithValue("@Email", email);
                    object result = idCmd.ExecuteScalar();
                    if (result == null) return NotFound();

                    EntityID = Convert.ToInt32(result);
                }

                // 2. Load reissue requests using EntityID
                string query = @"
                   

                SELECT
    r.RequestID, r.RequestType, r.Reason, r.CreatedAt,
    r.ApprovalStatus, r.DeliveryStatus, r.OSA_Remarks, r.DTS_Remarks,
    s.RollNumber, e.EmpCode
FROM ReissueRequests r
LEFT JOIN Students s ON s.StudentID = @EntityID AND r.Role = 'Student'
LEFT JOIN Employees e ON e.EmployeeID = @EntityID AND r.Role = 'Employee'
WHERE r.UserID = @UserID AND r.Role = @Role";


                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userId.Value);
                    cmd.Parameters.AddWithValue("@Role", Role);
                    cmd.Parameters.AddWithValue("@EntityID", EntityID);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        Requests.Add(new ReissueRequestView
                        {
                            RequestID = (int)reader["RequestID"],
                            RequestType = reader["RequestType"].ToString(),
                            Reason = reader["Reason"].ToString(),
                            CreatedAt = (DateTime)reader["CreatedAt"],
                            ApprovalStatus = reader["ApprovalStatus"]?.ToString() ?? "Pending",
                            DeliveryStatus = reader["DeliveryStatus"]?.ToString() ?? "Not Delivered",
                            OsaRemarks = reader["OSA_Remarks"]?.ToString() ?? "No Remarks",
                            DtsRemarks = reader["DTS_Remarks"]?.ToString() ?? "No Remarks",

                            CodeOrRoll = Role == "Student" ? reader["RollNumber"]?.ToString() : reader["EmpCode"]?.ToString()
                        });
                    }
                }
            }

            return Page();
        }

        public class ReissueRequestView
        {
            public int RequestID { get; set; }
            public string CodeOrRoll { get; set; }
            public string RequestType { get; set; }
            public string Reason { get; set; }
            public DateTime CreatedAt { get; set; }
            public string ApprovalStatus { get; set; }
            public string DeliveryStatus { get; set; }

            public string OsaRemarks { get; set; }

                 public string DtsRemarks { get; set; }
        }
    }
}
