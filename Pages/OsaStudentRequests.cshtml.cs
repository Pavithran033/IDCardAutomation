using IDCardAutomation.Models;
using IDCardAutomation.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace IDCardAutomation.Pages
{
    public class OsaStudentRequestsModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly EmailSender _emailSender;

        public List<ReissueRequestViewModel> Requests { get; set; } = new();

        public OsaStudentRequestsModel(IConfiguration config)
        {
            _config = config;
            _emailSender = new EmailSender(_config);
        }

        public void OnGet()
        {
            string role = HttpContext.Session.GetString("UserRole");
            if (role != "Osa") { Response.Redirect("/AccessDenied"); return; }

            using SqlConnection conn = new(_config.GetConnectionString("DefaultConnection"));
            conn.Open();





            string query = @"


SELECT DISTINCT
    R.RequestID, S.StudentID, S.RollNumber, S.FullName, 
    R.RequestType, R.Reason, R.CreatedAt, 
    R.OSAStatus, R.OSA_Remarks, R.ApprovalStatus, R.RequestCount,
    S.Email
FROM ReissueRequests R
JOIN Users U ON R.UserID = U.UserID
JOIN Students S ON U.Email = S.Email
WHERE U.Role = 'Student'
ORDER BY R.CreatedAt DESC";







            using SqlCommand cmd = new(query, conn);
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
                    OSAStatus = reader["OSAStatus"]?.ToString(),
                    Remarks = reader["OSA_Remarks"]?.ToString(),
                    ApprovalStatus = reader["ApprovalStatus"]?.ToString() ?? "Pending",
                    RequestCount = reader["RequestCount"] == DBNull.Value ? 1 : Convert.ToInt32(reader["RequestCount"]),
                    Email = reader["Email"].ToString()
                });
            }
        }

        public IActionResult OnPost()
        {
            string role = HttpContext.Session.GetString("UserRole");
            if (role != "Osa") return RedirectToPage("/AccessDenied");

            if (!int.TryParse(Request.Form["RequestID"], out int requestId))
            {
                TempData["Error"] = "Invalid request ID.";
                return RedirectToPage();
            }

            string action = Request.Form["ActionType"];
            string remarks = Request.Form["Remarks"];

            using SqlConnection conn = new(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            // Fetch student details for email
            string studentQuery = @"
                SELECT S.FullName, S.RollNumber, S.Email
                FROM ReissueRequests R
                JOIN Users U ON R.UserID = U.UserID
                JOIN Students S ON U.Email = S.Email
                WHERE R.RequestID = @RequestID";

            string studentName = "";
            string rollNumber = "";
            string studentEmail = "";

            using (SqlCommand studentCmd = new(studentQuery, conn))
            {
                studentCmd.Parameters.AddWithValue("@RequestID", requestId);
                using var reader = studentCmd.ExecuteReader();
                if (reader.Read())
                {
                    studentName = reader.GetString(0);
                    rollNumber = reader.GetString(1);
                    studentEmail = reader.GetString(2);
                }
            }

            // Optional: Check if already processed
            string checkQuery = "SELECT OSAStatus FROM ReissueRequests WHERE RequestID = @RequestID";
            using (SqlCommand checkCmd = new(checkQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@RequestID", requestId);
                var existingStatus = checkCmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(existingStatus) && existingStatus != "Pending")
                {
                    TempData["Error"] = "This request has already been processed.";
                    return RedirectToPage();
                }
            }

            // Determine new status and approval text
            string newStatus = action == "Approve" ? "Approved" :
                               action == "Reject" ? "Rejected" : null;

            string approvalStatus = action == "Approve" ? "Approved by OSA" :
                                    action == "Reject" ? "Rejected by OSA" : null;

            if (newStatus == null || approvalStatus == null)
            {
                TempData["Error"] = "Invalid action.";
                return RedirectToPage();
            }

            // Auto-generate remarks if empty
            if (string.IsNullOrWhiteSpace(remarks))
                remarks = approvalStatus;

            string updateQuery = @"
                UPDATE ReissueRequests 
                SET OSAStatus = @Status, 
                    OSA_Remarks = @Remarks, 
                    OSAApprovalTime = GETDATE(),
                    ApprovalStatus = @ApprovalStatus
                WHERE RequestID = @RequestID";

            using SqlCommand cmd = new(updateQuery, conn);
            cmd.Parameters.AddWithValue("@Status", newStatus);
            cmd.Parameters.AddWithValue("@Remarks", remarks);
            cmd.Parameters.AddWithValue("@ApprovalStatus", approvalStatus);
            cmd.Parameters.AddWithValue("@RequestID", requestId);
            cmd.ExecuteNonQuery();

            // Send Email Notification
            try
            {
                if (newStatus == "Approved")
                {
                    _emailSender.SendOsaApprovalToDTS(studentName, rollNumber);
                }
                else if (newStatus == "Rejected")
                {
                    _emailSender.SendOsaRejectionToStudent(studentEmail, studentName, remarks);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email sending failed: {ex.Message}");
            }

            TempData["Success"] = $"Request {newStatus.ToLower()} successfully.";
            return RedirectToPage();
        }
    }
}
