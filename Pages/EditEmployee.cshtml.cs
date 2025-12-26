using IDCardAutomation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace IDCardAutomation.Pages
{
    public class EditEmployeeModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public EditEmployeeModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public Employee Employee { get; set; } = new Employee();

        [BindProperty]
        public IFormFile? NewPhoto { get; set; }

        public string Base64Photo { get; set; }

        public IActionResult OnGet(int id)
        {
            if (id == 0) return NotFound();

            using SqlConnection conn = new(_configuration.GetConnectionString("DefaultConnection"));
            conn.Open();

            SqlCommand cmd = new("SELECT * FROM Employees WHERE EmployeeID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                Employee = new Employee
                {
                    EmployeeID = (int)reader["EmployeeID"],
                    EmpCode = reader["EmpCode"].ToString(),
                    FullName = reader["FullName"].ToString(),
                    DOB = (DateTime)reader["DOB"],
                    DOJ = (DateTime)reader["DOJ"],
                    DepartmentID = (int)reader["DepartmentID"],
                    Designation = reader["Designation"].ToString(),
                    PhoneNumber = reader["PhoneNumber"].ToString(),
                    Email = reader["Email"].ToString(),
                    BloodGroup = reader["BloodGroup"].ToString(),
                    Address = reader["Address"].ToString(),
                };

                if (!(reader["Photo"] is DBNull))
                {
                    byte[] photoBytes = (byte[])reader["Photo"];
                    Base64Photo = "data:image/jpeg;base64," + Convert.ToBase64String(photoBytes);
                }
                return Page();
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

            using SqlConnection conn = new(_configuration.GetConnectionString("DefaultConnection"));
            conn.Open();

            SqlCommand cmd = new(@"
                UPDATE Employees SET
                    EmpCode = @EmpCode,
                    FullName = @FullName,
                    DOB = @DOB,
                    DOJ = @DOJ,
                    DepartmentID = @DepartmentID,
                    Designation = @Designation,
                    PhoneNumber = @PhoneNumber,
                    Email = @Email,
                    BloodGroup = @BloodGroup,
                    Address = @Address
                 
                WHERE EmployeeID = @EmployeeID", conn);

            cmd.Parameters.AddWithValue("@EmployeeID", Employee.EmployeeID);
            cmd.Parameters.AddWithValue("@EmpCode", Employee.EmpCode);
            cmd.Parameters.AddWithValue("@FullName", Employee.FullName);
            cmd.Parameters.AddWithValue("@DOB", Employee.DOB);
            cmd.Parameters.AddWithValue("@DOJ", Employee.DOJ);
            cmd.Parameters.AddWithValue("@DepartmentID", Employee.DepartmentID);
            cmd.Parameters.AddWithValue("@Designation", Employee.Designation);
            cmd.Parameters.AddWithValue("@PhoneNumber", Employee.PhoneNumber);
            cmd.Parameters.AddWithValue("@Email", Employee.Email);
            cmd.Parameters.AddWithValue("@BloodGroup", Employee.BloodGroup);
            cmd.Parameters.AddWithValue("@Address", Employee.Address);
            
            cmd.Parameters.AddWithValue("@Photo", (object)DBNull.Value);

            cmd.ExecuteNonQuery();

            return RedirectToPage("/EmployeeStatus");
        }
    }
}
