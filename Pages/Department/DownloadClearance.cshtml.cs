using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using DinkToPdf;
using DinkToPdf.Contracts;
using System;
using System.IO;
using System.Text;

namespace YourNamespace.Pages.Department
{
    public class DownloadClearanceModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IConverter _converter;

        public DownloadClearanceModel(IConfiguration configuration, IConverter converter)
        {
            _configuration = configuration;
            _converter = converter;
        }

        public IActionResult OnGet()
        {
            // Session check
            string email = HttpContext.Session.GetString("Email");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToPage("/Login");
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            string employeeName = "", empCode = "", departmentName = "", designation = "", resignationDate = "";
            string deptStatus = "Pending", libStatus = "Pending", dtsStatus = "Pending";
            string requestId = "";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Fetch employee details
                string empQuery = @"SELECT EmpCode, Name, Department, Designation 
                                    FROM Employees WHERE Email = @Email";
                using (SqlCommand cmd = new SqlCommand(empQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            empCode = reader["EmpCode"].ToString();
                            employeeName = reader["Name"].ToString();
                            departmentName = reader["Department"].ToString();
                            designation = reader["Designation"].ToString();
                        }
                    }
                }

                // Fetch resignation date & RequestID
                string reqQuery = @"SELECT RequestID, ResignationDate 
                                    FROM NoDueRequest WHERE Email = @Email";
                using (SqlCommand cmd = new SqlCommand(reqQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            requestId = reader["RequestID"].ToString();
                            resignationDate = Convert.ToDateTime(reader["ResignationDate"])
                                              .ToString("dd-MM-yyyy");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(requestId))
                {
                    // Department clearance
                    deptStatus = GetStatus(conn, "DepartmentClearance", requestId);

                    // Library clearance
                    libStatus = GetStatus(conn, "LibraryClearance", requestId);

                    // DTS clearance
                    dtsStatus = GetStatus(conn, "DTSClearance", requestId);
                }
            }

            // Only allow download if all are Approved
            if (!(deptStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase) &&
                  libStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase) &&
                  dtsStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["ErrorMessage"] = "You can only download the clearance form after all statuses are Approved.";
                return RedirectToPage("/Department/ClearanceStatus");
            }

            // Prevent caching
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            // Build PDF content
            StringBuilder html = new StringBuilder();
            html.Append($@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; }}
                        h2 {{ text-align: center; }}
                        table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
                        td, th {{ border: 1px solid black; padding: 8px; text-align: left; }}
                    </style>
                </head>
                <body>
                    <h2>No Due Clearance Form</h2>
                    <p><strong>Employee Name:</strong> {employeeName}</p>
                    <p><strong>Emp Code:</strong> {empCode}</p>
                    <p><strong>Department:</strong> {departmentName}</p>
                    <p><strong>Designation:</strong> {designation}</p>
                    <p><strong>Resignation Date:</strong> {resignationDate}</p>

                    <table>
                        <tr>
                            <th>Department</th>
                            <th>Status</th>
                        </tr>
                        <tr>
                            <td>Department Clearance</td>
                            <td>{deptStatus}</td>
                        </tr>
                        <tr>
                            <td>Library Clearance</td>
                            <td>{libStatus}</td>
                        </tr>
                        <tr>
                            <td>DTS Clearance</td>
                            <td>{dtsStatus}</td>
                        </tr>
                    </table>
                </body>
                </html>
            ");

            // Convert to PDF
            var pdf = new HtmlToPdfDocument()
            {
                GlobalSettings = {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4
                },
                Objects = {
                    new ObjectSettings() {
                        HtmlContent = html.ToString()
                    }
                }
            };

            var file = _converter.Convert(pdf);
            return File(file, "application/pdf", "NoDueClearance.pdf");
        }

        private string GetStatus(SqlConnection conn, string tableName, string requestId)
        {
            string statusQuery = $"SELECT Status FROM {tableName} WHERE RequestID = @RequestID";
            using (SqlCommand cmd = new SqlCommand(statusQuery, conn))
            {
                cmd.Parameters.AddWithValue("@RequestID", requestId);
                return cmd.ExecuteScalar()?.ToString() ?? "Pending";
            }
        }
    }
}
