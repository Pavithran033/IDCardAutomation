using IDCardAutomation.Models;
using IDCardAutomation.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data.SqlClient;

namespace IDCardAutomation.Pages
{
    public class AddEmployeeModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly EmailSender _emailSender;

        public AddEmployeeModel(IConfiguration configuration, EmailSender emailSender)
        {
            _configuration = configuration;
            _emailSender = emailSender;
        }

        [BindProperty]
        public Employee Employee { get; set; }

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
            var userRole = HttpContext.Session.GetString("UserRole");
            int newEmployeeId;
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();



                string insertEmployeeSql = @"
    INSERT INTO Employees 
        (EmpCode, FullName, DOB, DepartmentID, Designation, PhoneNumber, Email, DOJ, BloodGroup, Address, Photo, IsAddedByHR)
    VALUES 
        (@EmpCode, @FullName, @DOB, @DepartmentID, @Designation, @PhoneNumber, @Email, @DOJ, @BloodGroup, @Address, @Photo, @IsAddedByHR);
    SELECT CAST(SCOPE_IDENTITY() AS INT);";


                using (SqlCommand cmd = new SqlCommand(insertEmployeeSql, conn))
                {
                    cmd.Parameters.AddWithValue("@EmpCode", Employee.EmpCode);
                    cmd.Parameters.AddWithValue("@FullName", Employee.FullName);
                    cmd.Parameters.AddWithValue("@DOB", Employee.DOB);
                    cmd.Parameters.AddWithValue("@DepartmentID", Employee.DepartmentID);
                    cmd.Parameters.AddWithValue("@Designation", Employee.Designation);
                    cmd.Parameters.AddWithValue("@PhoneNumber", Employee.PhoneNumber);
                    cmd.Parameters.AddWithValue("@Email", Employee.Email);
                    cmd.Parameters.AddWithValue("@DOJ", Employee.DOJ);
                    cmd.Parameters.AddWithValue("@BloodGroup", Employee.BloodGroup);
                    cmd.Parameters.AddWithValue("@Address", Employee.Address);
                    cmd.Parameters.AddWithValue("@Photo", photoBytes ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsAddedByHR", userRole == "Hr" ? 1 : 0);


                    newEmployeeId = (int)cmd.ExecuteScalar();
                }

                conn.Close();
            }



            //return RedirectToPage("/ViewEmployee", new { id = newEmployeeId });
            return RedirectToPage("/AddEmployee");



        }
    }
}
