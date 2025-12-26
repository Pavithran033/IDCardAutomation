using IDCardAutomation.Utils; // <-- for EmailSender
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace IDCardAutomation.Pages.Department
{
    public class ClearanceRequestsModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public ClearanceRequestsModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class RequestItem
        {
            public int RequestID { get; set; }
            public string EmpCode { get; set; }
            public string EmployeeName { get; set; }
            public string Designation { get; set; }
            public string Email { get; set; }
            public DateTime? ResignationDate { get; set; }
            public string Status { get; set; }
            public string Remarks { get; set; }
            public DateTime? UpdatedDate { get; set; }
        }

        public List<RequestItem> Requests { get; set; } = new();
        public string? Message { get; set; }
        public string Search { get; set; }
        public string StatusFilter { get; set; } = "All";

        public void OnGet(string search = "", string statusFilter = "All")
        {
            Message = TempData["Message"] as string;

            Search = search;
            StatusFilter = statusFilter;

            string deptEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(deptEmail))
            {
                Response.Redirect("/Account/SignIn");
                return;
            }

            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            conn.Open();

            // Get department ID from the logged-in department email
            string deptQuery = "SELECT DepartmentID FROM Departments WHERE DepartmentEmail = @Email";
            using var cmdDept = new SqlCommand(deptQuery, conn);
            cmdDept.Parameters.AddWithValue("@Email", deptEmail);
            object deptIdObj = cmdDept.ExecuteScalar();
            if (deptIdObj == null) return;
            int deptId = Convert.ToInt32(deptIdObj);

            string query = @"
                SELECT DC.RequestID, E.EmpCode, E.FullName, E.Designation, E.Email, 
                       NR.ResignationDate, DC.Status, DC.Remarks, DC.UpdatedDate
                FROM DepartmentClearance DC
                JOIN NoDueRequest NR ON DC.RequestID = NR.RequestID
                JOIN Employees E ON NR.EmployeeID = E.EmployeeID
                WHERE DC.DepartmentID = @DeptID";

            if (!string.IsNullOrEmpty(search))
                query += " AND (E.FullName LIKE @Search OR E.Email LIKE @Search OR E.EmpCode LIKE @Search)";
            if (statusFilter != "All")
                query += " AND DC.Status = @StatusFilter";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@DeptID", deptId);
            if (!string.IsNullOrEmpty(search))
                cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
            if (statusFilter != "All")
                cmd.Parameters.AddWithValue("@StatusFilter", statusFilter);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Requests.Add(new RequestItem
                {
                    RequestID = Convert.ToInt32(reader["RequestID"]),
                    EmpCode = reader["EmpCode"].ToString(),
                    EmployeeName = reader["FullName"].ToString(),
                    Designation = reader["Designation"].ToString(),
                    Email = reader["Email"].ToString(),
                    ResignationDate = reader["ResignationDate"] as DateTime?,
                    Status = reader["Status"].ToString(),
                    Remarks = reader["Remarks"]?.ToString() ?? "None",
                    UpdatedDate = reader["UpdatedDate"] as DateTime?
                });
            }
        }

        public IActionResult OnPost(int RequestID, string Remarks, string action)
        {
            string deptEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(deptEmail))
                return RedirectToPage("/Account/SignIn");

            using var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            conn.Open();

            // Which department is acting?
            int departmentId;
            using (var cmdDept = new SqlCommand("SELECT DepartmentID FROM Departments WHERE DepartmentEmail = @Email", conn))
            {
                cmdDept.Parameters.AddWithValue("@Email", deptEmail);
                var result = cmdDept.ExecuteScalar();
                if (result == null) return RedirectToPage();
                departmentId = Convert.ToInt32(result);
            }

            // Prevent editing after Approved
            const string statusCheckQuery = "SELECT Status FROM DepartmentClearance WHERE RequestID = @ReqID AND DepartmentID = @DeptID";
            using (var checkCmd = new SqlCommand(statusCheckQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@ReqID", RequestID);
                checkCmd.Parameters.AddWithValue("@DeptID", departmentId);
                var status = checkCmd.ExecuteScalar()?.ToString();
                if (status == "Approved")
                {
                    TempData["Message"] = "Request already approved. Changes are not allowed.";
                    return RedirectToPage();
                }
            }

            // Update this department's clearance
            string updateQuery = @"
                UPDATE DepartmentClearance
                SET Remarks = @Remarks,
                    UpdatedBy = @UpdatedBy,
                    UpdatedDate = @UpdatedDate
                    {0}
                WHERE RequestID = @ReqID AND DepartmentID = @DeptID";

            string statusUpdate = action == "approve" ? ", Status = 'Approved'" : "";
            using (var cmd = new SqlCommand(string.Format(updateQuery, statusUpdate), conn))
            {
                cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(Remarks) ? (object)DBNull.Value : Remarks);
                cmd.Parameters.AddWithValue("@UpdatedBy", deptEmail); // or "Department HOD" if you prefer a fixed label
                cmd.Parameters.AddWithValue("@UpdatedDate", DateTime.Now);
                cmd.Parameters.AddWithValue("@ReqID", RequestID);
                cmd.Parameters.AddWithValue("@DeptID", departmentId);
                cmd.ExecuteNonQuery();
            }

            // If approved here, check if all three clearances are approved
            if (action == "approve")
            {
                string checkSql = @"
                    SELECT 
                        CASE 
                            WHEN EXISTS (SELECT 1 FROM DepartmentClearance WHERE RequestID = @ReqID AND Status <> 'Approved')
                              OR EXISTS (SELECT 1 FROM LibraryClearance    WHERE RequestID = @ReqID AND Status <> 'Approved')
                              OR EXISTS (SELECT 1 FROM DTSClearance        WHERE RequestID = @ReqID AND Status <> 'Approved')
                            THEN 0 ELSE 1
                        END AS IsFullyApproved";

                int isFullyApproved;
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@ReqID", RequestID);
                    isFullyApproved = Convert.ToInt32(checkCmd.ExecuteScalar());
                }

                if (isFullyApproved == 1)
                {
                    // Update overall status if we haven't sent the final email yet
                    string updateOverallSql = @"
                        UPDATE NoDueRequest
                        SET OverallStatus = 'Approved'
                        WHERE RequestID = @ReqID AND ISNULL(FinalEmailSent, 0) = 0";

                    int rowsAffected;
                    using (var updateCmd = new SqlCommand(updateOverallSql, conn))
                    {
                        updateCmd.Parameters.AddWithValue("@ReqID", RequestID);
                        rowsAffected = updateCmd.ExecuteNonQuery();
                    }

                    if (rowsAffected > 0)
                    {
                        // Fetch employee info
                        string empEmail = "";
                        string empName = "";
                        using (var empCmd = new SqlCommand(@"
                            SELECT E.Email, E.FullName
                            FROM Employees E
                            JOIN NoDueRequest R ON E.EmployeeID = R.EmployeeID
                            WHERE R.RequestID = @ReqID", conn))
                        {
                            empCmd.Parameters.AddWithValue("@ReqID", RequestID);
                            using var rdr = empCmd.ExecuteReader();
                            if (rdr.Read())
                            {
                                empEmail = rdr["Email"]?.ToString() ?? "";
                                empName = rdr["FullName"]?.ToString() ?? "";
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(empEmail))
                        {
                            // Send final approval email & mark as sent
                            var emailSender = new EmailSender(_configuration);
                            emailSender.SendClearanceApprovedNotification(empEmail, empName);

                            using var markCmd = new SqlCommand(
                                "UPDATE NoDueRequest SET FinalEmailSent = 1 WHERE RequestID = @ReqID", conn);
                            markCmd.Parameters.AddWithValue("@ReqID", RequestID);
                            markCmd.ExecuteNonQuery();
                        }
                    }
                }
            }

            TempData["Message"] = action == "approve" ? "Request approved." : "Changes saved.";
            return RedirectToPage("/Department/ClearanceRequests");

        }
    }
}
