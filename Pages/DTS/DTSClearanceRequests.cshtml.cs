using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace IDCardAutomation.Pages.DTS
{
    public class DTSClearanceRequestsModel : PageModel
    {
        private readonly IConfiguration _config;
        public DTSClearanceRequestsModel(IConfiguration config) => _config = config;

        public class ClearanceRow
        {
            public int RequestID { get; set; }
            public string EmpCode { get; set; }
            public string Name { get; set; }
            public string Department { get; set; }
            public string Designation { get; set; }
            public string Email { get; set; }
            public DateTime ResignationDate { get; set; }
            public string Status { get; set; }
            public string Remarks { get; set; }
        }

        [BindProperty]
        public List<ClearanceRow> Requests { get; set; } = new();

        [BindProperty]
        public Dictionary<int, string> Remarks { get; set; } = new();

        public void OnGet()
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            var cmd = new SqlCommand(@"
SELECT NR.RequestID, E.EmpCode, E.FullName, D.DepartmentName, E.Designation, E.Email, NR.ResignationDate,
       C.Status, C.Remarks
FROM   DTSClearance C
JOIN   NoDueRequest NR ON NR.RequestID = C.RequestID
JOIN   Employees     E ON E.EmployeeID = NR.EmployeeID
JOIN   Departments   D ON D.DepartmentID = E.DepartmentID
ORDER BY NR.RequestedDate DESC", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Requests.Add(new ClearanceRow
                {
                    RequestID = reader.GetInt32(0),
                    EmpCode = reader.GetString(1),
                    Name = reader.GetString(2),
                    Department = reader.GetString(3),
                    Designation = reader.GetString(4),
                    Email = reader.GetString(5),
                    ResignationDate = reader.GetDateTime(6),
                    Status = reader.GetString(7),
                    Remarks = reader.IsDBNull(8) ? "None" : reader.GetString(8),
                    
                });
            }
        }

        public IActionResult OnPost()
        {
            string action = Request.Form["action"];
            if (string.IsNullOrEmpty(action))
                return RedirectToPage();

            var parts = action.Split('_');
            string command = parts[0];
            int requestId = int.Parse(parts[1]);
            string remarks = Request.Form[$"Remarks[{requestId}]"];
            string updatedBy = User.Identity?.Name ?? "Mr.Vimal";
            DateTime updatedDate = DateTime.Now;

            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            // 1. Update DTS clearance
            string sql = command == "approve"
                ? "UPDATE DTSClearance SET Status = 'Approved', Remarks = @Remarks, UpdatedBy = @UpdatedBy, UpdatedDate = @UpdatedDate WHERE RequestID = @RequestID"
                : "UPDATE DTSClearance SET Remarks = @Remarks, UpdatedBy = @UpdatedBy, UpdatedDate = @UpdatedDate WHERE RequestID = @RequestID";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks);
                cmd.Parameters.AddWithValue("@UpdatedBy", updatedBy);
                cmd.Parameters.AddWithValue("@UpdatedDate", updatedDate);
                cmd.Parameters.AddWithValue("@RequestID", requestId);
                cmd.ExecuteNonQuery();
            }

            // 2. Check if ALL clearances are approved
            string checkSql = @"
        SELECT 
            CASE 
                WHEN EXISTS (SELECT 1 FROM DepartmentClearance WHERE RequestID = @ReqID AND Status != 'Approved')
                  OR EXISTS (SELECT 1 FROM LibraryClearance WHERE RequestID = @ReqID AND Status != 'Approved')
                  OR EXISTS (SELECT 1 FROM DTSClearance WHERE RequestID = @ReqID AND Status != 'Approved')
                THEN 0 ELSE 1
            END AS IsFullyApproved";

            int isFullyApproved;
            using (var checkCmd = new SqlCommand(checkSql, conn))
            {
                checkCmd.Parameters.AddWithValue("@ReqID", requestId);
                isFullyApproved = Convert.ToInt32(checkCmd.ExecuteScalar());
            }

            if (isFullyApproved == 1)
            {
                // 3. Update NoDueRequest if not already finalized
                string updateSql = @"
            UPDATE NoDueRequest
            SET OverallStatus = 'Approved'
            WHERE RequestID = @ReqID AND FinalEmailSent = 0";

                using (var updateCmd = new SqlCommand(updateSql, conn))
                {
                    updateCmd.Parameters.AddWithValue("@ReqID", requestId);
                    int rowsAffected = updateCmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        // 4. Send email to employee
                        string empEmail = "";
                        string empName = "";

                        using (var empCmd = new SqlCommand(@"
                    SELECT E.Email, E.FullName
                    FROM Employees E
                    JOIN NoDueRequest R ON E.EmployeeID = R.EmployeeID
                    WHERE R.RequestID = @ReqID", conn))
                        {
                            empCmd.Parameters.AddWithValue("@ReqID", requestId);
                            using var reader = empCmd.ExecuteReader();
                            if (reader.Read())
                            {
                                empEmail = reader["Email"].ToString();
                                empName = reader["FullName"].ToString();
                            }
                        }

                        if (!string.IsNullOrEmpty(empEmail))
                        {
                            var emailSender = new Utils.EmailSender(_config);
                            emailSender.SendClearanceApprovedNotification(empEmail, empName);

                            // Mark email as sent
                            var markCmd = new SqlCommand("UPDATE NoDueRequest SET FinalEmailSent = 1 WHERE RequestID = @ReqID", conn);
                            markCmd.Parameters.AddWithValue("@ReqID", requestId);
                            markCmd.ExecuteNonQuery();
                        }
                    }
                }
            }

            return RedirectToPage();
        }

    }
}
