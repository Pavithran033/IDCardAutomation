using IDCardAutomation.Models;
using IDCardAutomation.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace IDCardAutomation.Pages
{
    public class EmployeeStatusModel : PageModel
    {
        public List<ReissueRequestViewModel> Requests { get; set; } = new();

        private readonly IConfiguration _config;
        private readonly EmailSender _emailSender;

        public EmployeeStatusModel(IConfiguration config, EmailSender emailSender)
        {
            _config = config;
            _emailSender = emailSender;
        }

        public void OnGet(string? SearchTerm, string? StatusFilter, bool reset = false)
        {
            if (reset)
            {
                // Reset filters
                SearchTerm = null;
                StatusFilter = null;
            }

            using SqlConnection conn = new(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            var query = @"
      

            SELECT R.RequestID, E.EmployeeID, E.EmpCode, E.FullName, R.RequestType, R.Reason, R.CreatedAt,
       R.DTSStatus, R.DeliveryStatus, R.ApprovalStatus, R.DTS_Remarks, R.RequestCount

        FROM ReissueRequests R
        JOIN Users U ON R.UserID = U.UserID
        JOIN Employees E ON U.Email = E.Email
        WHERE R.Role = 'Employee'
    ";




            var conditions = new List<string>();
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                conditions.Add("(E.EmpCode LIKE @Search OR E.FullName LIKE @Search)");
                parameters.Add(new SqlParameter("@Search", $"%{SearchTerm}%"));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                conditions.Add("R.ApprovalStatus = @StatusFilter");
                parameters.Add(new SqlParameter("@StatusFilter", StatusFilter));
            }

            if (conditions.Any())
                query += " AND " + string.Join(" AND ", conditions);

            query += " ORDER BY R.CreatedAt DESC";

            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddRange(parameters.ToArray());

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
                    DTSStatus = reader["DTSStatus"]?.ToString(),
                    DTS_Remarks = reader["DTS_Remarks"]?.ToString(),

                    DeliveryStatus = reader["DeliveryStatus"]?.ToString(),
                    ApprovalStatus = reader["ApprovalStatus"]?.ToString(),
                    RequestCount = reader["RequestCount"] == DBNull.Value ? 1 : Convert.ToInt32(reader["RequestCount"])
                });
            }
        }


        public IActionResult OnPost()
        {
            string role = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(role) || (role != "Dts" && role != "Admin"))
                return RedirectToPage("/AccessDenied");

            if (!int.TryParse(Request.Form["RequestID"], out int requestId))
            {
                TempData["Error"] = "Invalid Request ID.";
                return RedirectToPage();
            }

            string action = Request.Form["Action"];
            string remarks = Request.Form["Remarks"];

            string updateQuery = action switch
            {
                "Approve" => @"
                    UPDATE ReissueRequests
                    SET DTSStatus = 'Approved',
                        DTSApprovalTime = GETDATE(),
                        ApprovalStatus = 'Approved by DTS'
                    WHERE RequestID = @RequestID",

                "Reject" => @"
                    UPDATE ReissueRequests
                    SET DTSStatus = 'Rejected',
                        DTSApprovalTime = GETDATE(),
                        DTS_Remarks = @Remarks,
                        ApprovalStatus = 'Rejected by DTS'
                    WHERE RequestID = @RequestID",

                "Deliver" => @"
                    UPDATE ReissueRequests
                    SET DeliveryStatus = 'Delivered',
                        DeliveryTime = GETDATE(),
                        ApprovalStatus = 'Delivered by DTS'
                    WHERE RequestID = @RequestID",

                _ => null
            };

            if (updateQuery == null)
            {
                TempData["Error"] = "Invalid action.";
                return RedirectToPage();
            }

            using SqlConnection conn = new(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            using SqlCommand cmd = new(updateQuery, conn);
            cmd.Parameters.AddWithValue("@RequestID", requestId);

            if (action == "Reject")
            {
                if (string.IsNullOrWhiteSpace(remarks))
                    remarks = "Rejected by DTS";
                cmd.Parameters.AddWithValue("@Remarks", remarks);
            }

            cmd.ExecuteNonQuery();

            // ✅ Send email if action is Reject or Deliver
            if (action == "Reject" || action == "Deliver")
            {
                using SqlCommand getEmpCmd = new(@"
                    SELECT E.Email, E.FullName
                    FROM ReissueRequests R
                    JOIN Users U ON R.UserID = U.UserID
                    JOIN Employees E ON U.Email = E.Email
                    WHERE R.RequestID = @RequestID", conn);

                getEmpCmd.Parameters.AddWithValue("@RequestID", requestId);

                using SqlDataReader reader = getEmpCmd.ExecuteReader();
                if (reader.Read())
                {
                    string toEmail = reader["Email"].ToString();
                    string fullName = reader["FullName"].ToString();

                    if (action == "Deliver")
                    {
                        _emailSender.SendDeliveryNotificationToEmployee(toEmail, fullName);
                    }
                    else if (action == "Reject")
                    {
                        _emailSender.SendRejectionNotificationToEmployee(toEmail, fullName, remarks);
                    }
                }
            }

            TempData["Success"] = $"Request {action.ToLower()}ed successfully.";
            return RedirectToPage();
        }
    }
}
