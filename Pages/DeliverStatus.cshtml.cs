using IDCardAutomation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace IDCardAutomation.Pages
{
    public class DeliverStatusModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public List<ReissueRequestViewModel> Requests { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? EntityType { get; set; }  // "Student", "Employee", or null

        public DeliverStatusModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            string role = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(role) || role != "Deliver")
            {
                TempData["Error"] = "Access denied.";
                Response.Redirect("/AccessDenied");
                return;
            }

            using SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            conn.Open();

            if (string.IsNullOrEmpty(EntityType) || EntityType == "Student")
                LoadRequests("Student", conn);

            if (string.IsNullOrEmpty(EntityType) || EntityType == "Employee")
                LoadRequests("Employee", conn);
        }

        private void LoadRequests(string type, SqlConnection conn)
        {
            string query = type == "Student"
                ? @"
                    SELECT R.RequestID, S.StudentID, S.RollNumber, S.FullName, R.RequestType, R.Reason,
                           R.CreatedAt, R.ApprovalStatus, R.DeliveryStatus, R.UserID, 'Student' AS EntityType
                    FROM ReissueRequests R
                    JOIN Users U ON R.UserID = U.UserID
                    JOIN Students S ON U.Email = S.Email
                    WHERE R.ApprovalStatus = 'Approved' AND U.Role = 'Student'
                    ORDER BY R.CreatedAt DESC"
                : @"
                    SELECT R.RequestID, E.EmployeeID, E.EmpCode, E.FullName, R.RequestType, R.Reason,
                           R.CreatedAt, R.ApprovalStatus, R.DeliveryStatus, R.UserID, 'Employee' AS EntityType
                    FROM ReissueRequests R
                    JOIN Users U ON R.UserID = U.UserID
                    JOIN Employees E ON U.Email = E.Email
                    WHERE R.ApprovalStatus = 'Approved' AND U.Role = 'Employee'
                    ORDER BY R.CreatedAt DESC";

            using SqlCommand cmd = new SqlCommand(query, conn);
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Requests.Add(new ReissueRequestViewModel
                {
                    RequestID = reader.GetInt32(0),
                    EntityID = reader.GetInt32(1).ToString(),
                    CodeOrRoll = reader.GetString(2),
                    FullName = reader.GetString(3),
                    RequestType = reader.GetString(4),
                    Reason = reader.GetString(5),
                    CreatedAt = reader.GetDateTime(6),
                    ApprovalStatus = reader.GetString(7),
                    DeliveryStatus = reader.GetString(8),
                    UserID = reader.GetInt32(9),
                    EntityType = reader.GetString(10)
                });
            }
        }
    }
}
