using IDCardAutomation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace IDCardAutomation.Pages
{
    public class ViewStudentModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ViewStudentModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Student Student { get; set; }
        public string Base64Photo { get; set; }
        public string UserRole { get; set; }


        public IActionResult OnGet(int? id)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            int studentId = 0;
            // After loading student data successfully
            UserRole = HttpContext.Session.GetString("UserRole");
           


            if (id.HasValue)
            {
                studentId = id.Value;
            }
            else
            {
                // Try to fetch based on logged-in user
                string email = HttpContext.Session.GetString("UserEmail");
                string role = HttpContext.Session.GetString("UserRole");

                if (string.IsNullOrEmpty(email) || role != "Student")
                    return RedirectToPage("/Account/SignIn");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string getIdSql = "SELECT StudentID FROM Students WHERE Email = @Email";
                    SqlCommand getIdCmd = new SqlCommand(getIdSql, conn);
                    getIdCmd.Parameters.AddWithValue("@Email", email);

                    conn.Open();
                    object result = getIdCmd.ExecuteScalar();
                    if (result == null) return NotFound();

                    studentId = Convert.ToInt32(result);
                }
            }

            // Fetch full student record
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string sql = "SELECT * FROM Students WHERE StudentID = @StudentID";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@StudentID", studentId);

                conn.Open();
                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    Student = new Student
                    {
                        StudentID = (int)reader["StudentID"],
                        RollNumber = reader["RollNumber"].ToString(),
                        FullName = reader["FullName"].ToString(),
                        DOB = (DateTime)reader["DOB"],
                        DepartmentID = (int)reader["DepartmentID"],
                        YearOfStudy = reader["YearOfStudy"].ToString(),
                        PhoneNumber = reader["PhoneNumber"].ToString(),
                        Email = reader["Email"].ToString(),
                        BloodGroup = reader["BloodGroup"].ToString(),
                        Address = reader["Address"].ToString(),
                        HostelDayscholar = reader["HostelDayscholar"].ToString()
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
