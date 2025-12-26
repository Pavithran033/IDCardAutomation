using IDCardAutomation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SqlClient;

namespace IDCardAutomation.Pages
{
    public class AddStudentModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public AddStudentModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        [BindProperty]
        public Student Student { get; set; }

        [BindProperty]
        public IFormFile Photo { get; set; }
        public void OnGet()
        {
        }
        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("Form posted");
            byte[] photoBytes = null;

            if (Photo != null && Photo.Length > 0)
            {
                using (var ms = new MemoryStream())
                {
                    await Photo.CopyToAsync(ms);
                    photoBytes = ms.ToArray();
                }
            }

            int newStudentId;

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string sql = @"INSERT INTO Students 
                               (RollNumber, FullName, DOB, DepartmentID,YearOfStudy, PhoneNumber, Email, BloodGroup, Address,Photo,HostelDayscholar)
                               VALUES 
                               (@RollNumber, @FullName, @DOB, @DepartmentID,@YearOfStudy, @PhoneNumber, @Email, @BloodGroup, @Address,@Photo,@HostelDayscholar);SELECT CAST(SCOPE_IDENTITY() AS INT);";

                SqlCommand cmd = new SqlCommand(sql, conn);
                
                cmd.Parameters.AddWithValue("@RollNumber", Student.RollNumber);
                cmd.Parameters.AddWithValue("@FullName", Student.FullName);
                cmd.Parameters.AddWithValue("@DOB", Student.DOB);
                cmd.Parameters.AddWithValue("@DepartmentID", Student.DepartmentID);
                cmd.Parameters.AddWithValue("@YearOfStudy", Student.YearOfStudy);
                cmd.Parameters.AddWithValue("@PhoneNumber", Student.PhoneNumber);
                cmd.Parameters.AddWithValue("@Email", Student.Email);
                cmd.Parameters.AddWithValue("@BloodGroup", Student.BloodGroup);
                cmd.Parameters.AddWithValue("@Address", Student.Address);
                cmd.Parameters.AddWithValue("@Photo", photoBytes ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@HostelDayscholar", Student.HostelDayscholar);
  
                Console.WriteLine("Photo bytes length: " + (photoBytes?.Length ?? 0));

                System.Diagnostics.Debug.WriteLine("Executing SQL command: " + cmd.CommandText);
                conn.Open();
                
                newStudentId = (int)cmd.ExecuteScalar();
                conn.Close();
            }

            return RedirectToPage("/AddStudent");

           // return RedirectToPage("/ViewStudent", new { id = newStudentId });



        }
        }

    }
