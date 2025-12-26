using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace IDCardAutomation.Pages.HR
{
    public class PendingResignationsModel : PageModel
    {
        private readonly IConfiguration _config;

        [BindProperty(SupportsGet = true)]
        public string Search { get; set; } = "";

        [BindProperty(SupportsGet = true)]
        public string Filter { get; set; } = "All"; // All / Pending / Updated

        public List<ResignationItem> Requests { get; set; } = new();

        public class ResignationItem
        {
            public int RequestID { get; set; }
            public string FullName { get; set; }
            public string EmpCode { get; set; }
            public string Email { get; set; }
            public string Designation { get; set; }
            public string DepartmentName { get; set; }
            public DateTime? ResignationDate { get; set; }
        }

        public PendingResignationsModel(IConfiguration config) => _config = config;

        public void OnGet()
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();

            

            string sql = @"
SELECT 
    R.RequestID, 
    E.FullName, 
    E.EmpCode, 
    E.Email,
E.Designation,
    R.ResignationDate,
    D.DepartmentName
FROM NoDueRequest R
JOIN Employees E ON R.EmployeeID = E.EmployeeID
JOIN Departments D ON E.DepartmentID = D.DepartmentID
WHERE 
    (@Search = '' OR 
     E.FullName LIKE '%' + @Search + '%' OR 
     E.EmpCode LIKE '%' + @Search + '%' OR 
     E.Email LIKE '%' + @Search + '%' OR
 E.Designation LIKE '%' + @Search + '%')
 AND (
        @Filter = 'All' OR 
        (@Filter = 'Pending' AND R.ResignationDate IS NULL) OR
        (@Filter = 'Updated' AND R.ResignationDate IS NOT NULL)
    )
ORDER BY R.RequestID DESC";



            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Search", Search ?? "");
            cmd.Parameters.AddWithValue("@Filter", Filter ?? "All");

            
using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Requests.Add(new ResignationItem
                {
                    RequestID = Convert.ToInt32(reader["RequestID"]),
                    EmpCode = reader["EmpCode"].ToString(),
                    FullName = reader["FullName"].ToString(),
                    DepartmentName = reader["DepartmentName"].ToString(),
                    Designation = reader["Designation"].ToString(),
                    Email = reader["Email"].ToString(),
                    ResignationDate = reader["ResignationDate"] == DBNull.Value ? null : (DateTime?)reader["ResignationDate"]
                });
            }
        }
        public IActionResult OnPostUpdate(int requestId, DateTime resignationDate)
        {
            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            conn.Open();
            using var tr = conn.BeginTransaction();

            try
            {
                // 1. Update resignation date in NoDueRequest
                var cmdUpdate = new SqlCommand("UPDATE NoDueRequest SET ResignationDate = @Date WHERE RequestID = @ID", conn, tr);
                cmdUpdate.Parameters.AddWithValue("@Date", resignationDate);
                cmdUpdate.Parameters.AddWithValue("@ID", requestId);
                cmdUpdate.ExecuteNonQuery();

                // 2. Update Employee table
                var cmdEmpDate = new SqlCommand(@"
UPDATE E SET E.DateOfResignation = @Date
FROM Employees E
INNER JOIN NoDueRequest R ON R.EmployeeID = E.EmployeeID
WHERE R.RequestID = @ID", conn, tr);
                cmdEmpDate.Parameters.AddWithValue("@Date", resignationDate);
                cmdEmpDate.Parameters.AddWithValue("@ID", requestId);
                cmdEmpDate.ExecuteNonQuery();

                // 3. Insert into clearance tables
                string empEmail = "";
                int departmentId = 0;

                using (var cmdEmp = new SqlCommand(@"
SELECT E.Email, E.DepartmentID
FROM Employees E
JOIN NoDueRequest R ON E.EmployeeID = R.EmployeeID
WHERE R.RequestID = @ID", conn, tr))
                {
                    cmdEmp.Parameters.AddWithValue("@ID", requestId);
                    using var reader = cmdEmp.ExecuteReader();
                    if (reader.Read())
                    {
                        empEmail = reader["Email"].ToString();
                        departmentId = Convert.ToInt32(reader["DepartmentID"]);
                    }
                }

                void InsertClearance(string table)
                {
                    var insertCmd = new SqlCommand($"INSERT INTO {table} (RequestID) VALUES (@ID)", conn, tr);
                    insertCmd.Parameters.AddWithValue("@ID", requestId);
                    insertCmd.ExecuteNonQuery();
                }

                // DepartmentClearance
                var cmdDep = new SqlCommand("INSERT INTO DepartmentClearance (RequestID, DepartmentID) VALUES (@ID, @DeptID)", conn, tr);
                cmdDep.Parameters.AddWithValue("@ID", requestId);
                cmdDep.Parameters.AddWithValue("@DeptID", departmentId);
                cmdDep.ExecuteNonQuery();

                InsertClearance("LibraryClearance");
                InsertClearance("DTSClearance");

                tr.Commit();

                // 4. Send Emails
                var emailSender = new Utils.EmailSender(_config);
                string subject = "Clearance Request Activated";
                string body = $"Clearance Request ID: {requestId} is now active. Please update clearance status.";

                string deptEmail = "";
                using (var cmd = new SqlCommand("SELECT DepartmentEmail FROM Departments WHERE DepartmentID = @Dept", conn))
                {
                    cmd.Parameters.AddWithValue("@Dept", departmentId);
                    deptEmail = cmd.ExecuteScalar()?.ToString();
                }

                string dtsEmail = _config["EmailSettings:DtsEmail"];
                string libraryEmail = _config["EmailSettings:LibraryEmail"];

                if (!string.IsNullOrEmpty(deptEmail)) emailSender.Send(deptEmail, subject, body);
                if (!string.IsNullOrEmpty(dtsEmail)) emailSender.Send(dtsEmail, subject, body);
                if (!string.IsNullOrEmpty(libraryEmail)) emailSender.Send(libraryEmail, subject, body);

                // ✅ New: Send email to Employee about resignation update
                string empName = Requests.FirstOrDefault(r => r.RequestID == requestId)?.FullName ?? "Employee";
                emailSender.SendResignationUpdatedToEmployee(empEmail, empName, resignationDate);

                TempData["Message"] = "Resignation updated. Departments and employee notified.";

            }
            catch (Exception ex)
            {
                tr.Rollback();
                TempData["Message"] = "Error updating: " + ex.Message;
            }

            return RedirectToPage();
        }
    }
}
