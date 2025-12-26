using Azure.Core;
using IDCardAutomation.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace IDCardAutomation.Pages
{
    public class ClearanceFormModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly EmailSender _emailSender;

        public ClearanceFormModel(IConfiguration configuration, EmailSender emailSender)
        {
            _configuration = configuration;
            _emailSender = emailSender;
        }


        public string EmpCode { get; set; }
        public string FullName { get; set; }

        public IActionResult OnGet()
        {
            string email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToPage("/Account/SignIn");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT EmpCode, FullName FROM Employees WHERE Email = @Email";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Email", email);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        EmpCode = reader["EmpCode"].ToString();
                        FullName = reader["FullName"].ToString();
                    }
                }
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            string email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return RedirectToPage("/Account/SignIn");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            int requestId = 0;
            string hrEmail = string.Empty;
            string employeeName = string.Empty;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // 1. Get EmployeeID and FullName
                    string getEmpIdQuery = "SELECT EmployeeID, FullName FROM Employees WHERE Email = @Email";
                    int employeeId;

                    using (SqlCommand cmdEmp = new SqlCommand(getEmpIdQuery, conn, transaction))
                    {
                        cmdEmp.Parameters.AddWithValue("@Email", email);
                        using var reader = cmdEmp.ExecuteReader();
                        if (!reader.Read())
                        {
                            transaction.Rollback();
                            TempData["Message"] = "Error: You are not registered as an employee.";
                            return RedirectToPage("/ClearanceForm");
                        }

                        employeeId = Convert.ToInt32(reader["EmployeeID"]);
                        employeeName = reader["FullName"].ToString();
                    }

                    // 2. Get HR Email from Users table where Role = 'Hr'
                    string getHrEmailQuery = "SELECT TOP 1 Email FROM Users WHERE Role = 'Hr'";
                    using (SqlCommand cmdHr = new SqlCommand(getHrEmailQuery, conn, transaction))
                    {
                        object result = cmdHr.ExecuteScalar();
                        hrEmail = result?.ToString();
                    }

                    // 3. Check if request already exists
                    string checkExistingRequest = "SELECT COUNT(*) FROM NoDueRequest WHERE EmployeeID = @EmployeeID";
                    using (SqlCommand cmdCheck = new SqlCommand(checkExistingRequest, conn, transaction))
                    {
                        cmdCheck.Parameters.AddWithValue("@EmployeeID", employeeId);
                        int existingRequests = (int)cmdCheck.ExecuteScalar();

                        if (existingRequests > 0)
                        {
                            transaction.Rollback();
                            TempData["Message"] = "Error: You have already submitted a clearance request.";
                            return RedirectToPage("/ClearanceForm");
                        }
                    }

                    // 4. Insert NoDueRequest
                    string insertRequest = @"
                INSERT INTO NoDueRequest (EmployeeID, RequestedDate)
                OUTPUT INSERTED.RequestID
                VALUES (@EmployeeID, GETDATE());";

                    using (SqlCommand cmdRequest = new SqlCommand(insertRequest, conn, transaction))
                    {
                        cmdRequest.Parameters.AddWithValue("@EmployeeID", employeeId);
                        requestId = Convert.ToInt32(cmdRequest.ExecuteScalar());
                    }

                    transaction.Commit();

                    // 5. ✅ Send Email to HR after request is created
                    if (!string.IsNullOrEmpty(hrEmail))
                    {
                        string subject = "Update Resignation Date for Employee Clearance";
                        string body = $@"
                    Dear HR,<br/><br/>
                    A clearance form has been submitted by <b>{employeeName}</b> (Employee Email: {email}).<br/><br/>
                    Please update their resignation date in the system for further processing.<br/><br/>
                    Regards,<br/>
                    Clearance Automation System";

                        _emailSender.Send(hrEmail, subject, body);
                    }

                    TempData["Message"] = "No Due Request submitted successfully and email sent to HR.";
                }
                catch (Exception ex)
                {
                    try { transaction.Rollback(); } catch { }
                    TempData["Message"] = "Request failed: " + ex.Message;
                }

                return RedirectToPage("/ClearanceForm");
            }
        }


    }
}
