using IDCardAutomation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace IDCardAutomation.Pages
{
    public class ViewEmployeeModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ViewEmployeeModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Employee Employee { get; set; }
        public string Base64Photo { get; set; }
        public string UserRole { get; set; }



        public IActionResult OnGet(int? id)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            int employeeId = 0;
            UserRole = HttpContext.Session.GetString("UserRole");

            if (id.HasValue)
            {
                employeeId = id.Value;
            }
            else
            {
                // Get employee ID using session
                string email = HttpContext.Session.GetString("UserEmail");
                string role = HttpContext.Session.GetString("UserRole");

                if (string.IsNullOrEmpty(email) || role != "Employee")
                    return RedirectToPage("/Account/SignIn");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string getIdSql = "SELECT EmployeeID FROM Employees WHERE Email = @Email";
                    SqlCommand getIdCmd = new SqlCommand(getIdSql, conn);
                    getIdCmd.Parameters.AddWithValue("@Email", email);

                    conn.Open();
                    object result = getIdCmd.ExecuteScalar();
                    if (result == null) return NotFound();

                    employeeId = Convert.ToInt32(result);
                }
            }

            // Fetch employee data
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string sql = "SELECT * FROM Employees WHERE EmployeeID = @EmployeeID";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@EmployeeID", employeeId);

                conn.Open();
                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    Employee = new Employee
                    {
                        EmployeeID = (int)reader["EmployeeID"],
                        EmpCode = reader["EmpCode"].ToString(),
                        FullName = reader["FullName"].ToString(),
                        DOB = (DateTime)reader["DOB"],
                        DepartmentID = (int)reader["DepartmentID"],
                        Designation = reader["Designation"].ToString(),
                        PhoneNumber = reader["PhoneNumber"].ToString(),
                        Email = reader["Email"].ToString(),
                        DOJ = (DateTime)reader["DOJ"],
                        BloodGroup = reader["BloodGroup"].ToString(),
                        Address = reader["Address"].ToString()
                    };

                    if (!(reader["Photo"] is DBNull))
                    {
                        byte[] photoBytes = (byte[])reader["Photo"];
                        Base64Photo = "data:image/jpeg;base64," + Convert.ToBase64String(photoBytes);
                    }
                }
                else
                {
                    return NotFound();
                }
            }

            return Page();
        }
    }
}
