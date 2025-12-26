using IDCardAutomation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
namespace IDCardAutomation.Pages
{
    public class EditStudentModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public EditStudentModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]

        public IFormFile? NewPhoto { get; set; }

        [BindProperty]
        public Student Student { get; set; } = new Student();


       
        public string Base64Photo { get; set; }

        public IActionResult OnGet(int id)
        {
            if (id == 0) return NotFound();

            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM Students WHERE StudentID = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);

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
                    return Page();
                }
            }

            return NotFound();
        }


        public async Task<IActionResult> OnPostAsync()
        {
            byte[]? photoBytes = null;

            if (NewPhoto != null && NewPhoto.Length > 0)
            {
                using var ms = new MemoryStream();
                await NewPhoto.CopyToAsync(ms);
                photoBytes = ms.ToArray();
            }

       
            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    UPDATE Students SET 
                        RollNumber = @RollNumber,
                        FullName = @FullName,
                        DOB = @DOB,
                        DepartmentID = @DepartmentID,
                       YearOfStudy = @YearOfStudy,
                        PhoneNumber = @PhoneNumber,
                        Email = @Email,
                        BloodGroup = @BloodGroup,
                        Address = @Address,
             
                        HostelDayscholar = @HostelDayscholar
                    WHERE StudentID = @StudentID", conn);

                cmd.Parameters.AddWithValue("@StudentID", Student.StudentID);
                cmd.Parameters.AddWithValue("@RollNumber", Student.RollNumber);
                cmd.Parameters.AddWithValue("@FullName", Student.FullName);
                cmd.Parameters.AddWithValue("@DOB", Student.DOB);
                cmd.Parameters.AddWithValue("@DepartmentID", Student.DepartmentID);
                cmd.Parameters.AddWithValue("@YearOfStudy", Student.YearOfStudy);
                cmd.Parameters.AddWithValue("@PhoneNumber", Student.PhoneNumber);
                cmd.Parameters.AddWithValue("@Email", Student.Email);
                cmd.Parameters.AddWithValue("@BloodGroup", Student.BloodGroup);
                cmd.Parameters.AddWithValue("@Address", Student.Address);
                cmd.Parameters.AddWithValue("@HostelDayscholar", Student.HostelDayscholar);
                cmd.Parameters.AddWithValue("@Photo", (object)DBNull.Value); // Assuming photo is not being updated here

                cmd.ExecuteNonQuery();
            }

            return RedirectToPage("/OsaStudentRequests"); // or wherever you want to go after update
        }
    }
}
