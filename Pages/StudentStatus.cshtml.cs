using IDCardAutomation.Models;
using IDCardAutomation.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace IDCardAutomation.Pages
{
    public class StudentStatusModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly EmailSender _emailSender;
        public List<ReissueRequestViewModel> Requests { get; set; } = new();

        public StudentStatusModel(IConfiguration config, EmailSender emailSender)
        {
            _config = config;
            _emailSender = emailSender;
        }

        public void OnGet()
        {
            string role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || role != "Admin")
            {
                Response.Redirect("/AccessDenied");
                return;
            }

            using SqlConnection conn = new(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            string query = @"
                SELECT R.RequestID, S.StudentID, S.RollNumber, S.FullName,
                       R.RequestType, R.Reason, R.CreatedAt,
                       R.DeliveryStatus, R.ApprovalStatus, R.DeliveryTime, R.RequestCount
                FROM ReissueRequests R
                JOIN Users U ON R.UserID = U.UserID
                JOIN Students S ON U.Email = S.Email
                WHERE R.OSAStatus = 'Approved'
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
                    DeliveryStatus = reader["DeliveryStatus"].ToString(),
                    ApprovalStatus = reader["ApprovalStatus"].ToString(),
                    RequestCount = reader["RequestCount"] == DBNull.Value ? 1 : Convert.ToInt32(reader["RequestCount"])
                });
            }
        }

        public IActionResult OnPost()
        {
            string role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || role != "Admin")
                return RedirectToPage("/AccessDenied");

            if (!int.TryParse(Request.Form["RequestID"], out int requestId))
            {
                TempData["Error"] = "Invalid Request ID.";
                return RedirectToPage();
            }

            string action = Request.Form["ActionType"];
            if (action == "Deliver")
            {
                using SqlConnection conn = new(_config.GetConnectionString("DefaultConnection"));
                conn.Open();

                // 1️⃣ Update DB
                string updateQuery = @"
                    UPDATE ReissueRequests
                    SET DeliveryStatus = 'Delivered',
                        DeliveryTime   = GETDATE(),
                        ApprovalStatus = 'Delivered by DTS'
                    WHERE RequestID   = @RequestID";

                using (SqlCommand cmd = new(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@RequestID", requestId);
                    cmd.ExecuteNonQuery();
                }

                // 2️⃣ Fetch student e‑mail & name for notification
                string fetchQuery = @"
                    SELECT S.Email, S.FullName
                    FROM ReissueRequests R
                    JOIN Users U   ON R.UserID = U.UserID
                    JOIN Students S ON U.Email = S.Email
                    WHERE R.RequestID = @RequestID";

                string email = string.Empty;
                string name = string.Empty;
                using (SqlCommand fcmd = new(fetchQuery, conn))
                {
                    fcmd.Parameters.AddWithValue("@RequestID", requestId);
                    using SqlDataReader r = fcmd.ExecuteReader();
                    if (r.Read())
                    {
                        email = r.GetString(0);
                        name = r.GetString(1);
                    }
                }

                // 3️⃣ Send email if we have an address
                if (!string.IsNullOrWhiteSpace(email))
                {
                    try
                    {
                        _emailSender.SendDeliveryReadyToStudent(email, name);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send delivery mail: {ex.Message}");
                    }
                }

                TempData["Success"] = "Request marked as delivered.";
            }

            return RedirectToPage();
        }
    }
}
