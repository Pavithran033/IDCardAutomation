using Azure.Core;
using IDCardAutomation.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;
using System.Data;

namespace IDCardAutomation.Pages.Department
{
    public class ClearanceStatusModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ClearanceStatusModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class EmployeeDetailsModel
        {
            public int EmployeeID { get; set; }
            public string EmpCode { get; set; }
            public string Name { get; set; }
            public string DepartmentName { get; set; }
            public string Designation { get; set; }
            public string Email { get; set; }
            public byte[] Photo { get; set; }
            public DateTime? ResignationDate { get; set; }
        }

        public class DepartmentClearanceModel
        {
            public int SNo { get; set; }
            public string DepartmentName { get; set; }
            public string Status { get; set; }
            public string Remarks { get; set; }
            public string UpdatedBy { get; set; }
            public DateTime? UpdatedDate { get; set; }
        }

        public class LibraryClearanceInfo
        {
            public string Remarks { get; set; }
            public string Status { get; set; }
            public string UpdatedBy { get; set; }
            public DateTime? UpdatedDate { get; set; }
        }

        public class DTSClearanceInfo
        {
            public string Remarks { get; set; }
            public string Status { get; set; }
            public string UpdatedBy { get; set; }
            public DateTime? UpdatedDate { get; set; }
        }

        public EmployeeDetailsModel EmployeeDetails { get; set; }
        public DepartmentClearanceModel DepartmentClearances { get; set; } = new();
        public LibraryClearanceInfo LibraryClearance { get; set; } = new();
        public DTSClearanceInfo DTSClearance { get; set; } = new();

        public bool IsDownloadAvailable { get; set; }

        public IActionResult OnGet()
        {
            string email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "Session expired or not logged in.";
                return RedirectToPage("/SignIn");
            }

            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var con = new SqlConnection(connStr))
            {
                con.Open();

                // Fetch Employee Info
                var cmdEmp = new SqlCommand(@"SELECT e.EmployeeID, e.EmpCode, e.FullName, e.Designation, 
                                                     e.Email, e.Photo, e.DateOfResignation, d.DepartmentName 
                                              FROM Employees e
                                              JOIN Departments d ON e.DepartmentID = d.DepartmentID
                                              WHERE e.Email = @Email", con);
                cmdEmp.Parameters.AddWithValue("@Email", email);

                int employeeID = 0;
                using (var reader = cmdEmp.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        employeeID = Convert.ToInt32(reader["EmployeeID"]);
                        EmployeeDetails = new EmployeeDetailsModel
                        {
                            EmployeeID = employeeID,
                            EmpCode = reader["EmpCode"].ToString(),
                            Name = reader["FullName"].ToString(),
                            DepartmentName = reader["DepartmentName"].ToString(),
                            Designation = reader["Designation"].ToString(),
                            Email = reader["Email"].ToString(),
                            Photo = reader["Photo"] as byte[],
                            ResignationDate = reader["DateOfResignation"] as DateTime?
                        };
                    }
                    else
                    {
                        TempData["Error"] = "Employee not found.";
                        return RedirectToPage("/SignIn");
                    }
                }

                // Get RequestID and OverallStatus from NoDueRequest
                int requestID = 0;
                string overallStatus = "Pending";

                var cmdReq = new SqlCommand("SELECT TOP 1 RequestID, OverallStatus FROM NoDueRequest WHERE EmployeeID = @EmpID ORDER BY RequestedDate DESC", con);
                cmdReq.Parameters.AddWithValue("@EmpID", employeeID);

                using (var reader = cmdReq.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        requestID = Convert.ToInt32(reader["RequestID"]);
                        overallStatus = reader["OverallStatus"]?.ToString() ?? "Pending";
                    }
                    else
                    {
                        return Page(); // no clearance request found
                    }
                }

                // Fetch Department Clearance (latest row per dept, if needed)
                var cmdClr = new SqlCommand(@"
                    SELECT dc.Status, dc.Remarks,dc.UpdatedBy, dc.UpdatedDate, d.DepartmentName
                    FROM DepartmentClearance dc
                    JOIN Departments d ON dc.DepartmentID = d.DepartmentID
                    WHERE dc.RequestID = @ReqID
                    ORDER BY d.ClearanceOrder", con);
                cmdClr.Parameters.AddWithValue("@ReqID", requestID);

                using (var reader = cmdClr.ExecuteReader())
                {
                    int sno = 1;
                    while (reader.Read())
                    {
                        DepartmentClearances = new DepartmentClearanceModel
                        {
                            SNo = sno++,
                            DepartmentName = reader["DepartmentName"].ToString(),
                            Status = reader["Status"].ToString(),
                            Remarks = string.IsNullOrEmpty(reader["Remarks"]?.ToString()) ? "None" : reader["Remarks"].ToString(),
                            UpdatedBy = reader["UpdatedBy"]?.ToString() ?? "Department HOD",
                            UpdatedDate = reader["UpdatedDate"] as DateTime?
                        };
                    }
                }

                // Fetch Library Clearance
                var cmdLib = new SqlCommand(@"SELECT Remarks, Status, UpdatedBy, UpdatedDate 
                              FROM LibraryClearance 
                              WHERE RequestID = @RequestID", con);
                cmdLib.Parameters.AddWithValue("@RequestID", requestID);
                using (var reader = cmdLib.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        LibraryClearance = new LibraryClearanceInfo
                        {
                            Remarks = reader["Remarks"]?.ToString() ?? "None",
                            Status = reader["Status"]?.ToString() ?? "Pending",
                            UpdatedBy = reader["UpdatedBy"]?.ToString() ?? "N/A",
                            UpdatedDate = reader["UpdatedDate"] as DateTime?
                        };
                    }
                }

                // Fetch DTS Clearance
                var cmdDTS = new SqlCommand(@"SELECT Remarks, Status, UpdatedBy, UpdatedDate 
                              FROM DTSClearance 
                              WHERE RequestID = @RequestID", con);
                cmdDTS.Parameters.AddWithValue("@RequestID", requestID);
                using (var reader = cmdDTS.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        DTSClearance = new DTSClearanceInfo
                        {
                            Remarks = reader["Remarks"]?.ToString() ?? "None",
                            Status = reader["Status"]?.ToString() ?? "Pending",
                            UpdatedBy = reader["UpdatedBy"]?.ToString() ?? "N/A",
                            UpdatedDate = reader["UpdatedDate"] as DateTime?
                        };
                    }
                }

                // ✅ Instead of checking all statuses individually, rely on NoDueRequest.OverallStatus
                IsDownloadAvailable = overallStatus == "Approved";

                return Page();
            }
        }
    }
}
