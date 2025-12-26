using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using IDCardAutomation.Utils;

namespace IDCardAutomation.Services
{
    public class AuthService
    {
        private readonly string _connectionString;
        private readonly EmailSender _emailSender;

        public AuthService(IConfiguration config, EmailSender emailSender)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
            _emailSender = emailSender;
        }

        public bool Register(string email, string password, string role)
        {
            using var con = new SqlConnection(_connectionString);
            con.Open();

            using var transaction = con.BeginTransaction();

            try
            {
                // Check if user already exists
                using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", con, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@Email", email);
                    int existingUsers = (int)checkCmd.ExecuteScalar();
                    if (existingUsers > 0)
                    {
                        transaction.Rollback();
                        return false; // User already exists
                    }
                }

                // Insert new user and get new UserID
                int newUserId;
                using (var insertCmd = new SqlCommand(
                    "INSERT INTO Users (Email, PasswordHash, Role) OUTPUT INSERTED.UserID VALUES (@Email, @PasswordHash, @Role)", con, transaction))
                {
                    insertCmd.Parameters.AddWithValue("@Email", email);
                    insertCmd.Parameters.AddWithValue("@PasswordHash", PasswordHasher.HashPassword(password));
                    insertCmd.Parameters.AddWithValue("@Role", role);

                    newUserId = (int)insertCmd.ExecuteScalar();
                }

                // If role is Employee, check Employees table and insert reissue request if needed
                if (role == "Employee")
                {
                    int empCount;
                    bool isAddedByHR = false;

                    // ✅ Check if employee exists and was added by HR
                    using (var empCheckCmd = new SqlCommand(
                        "SELECT COUNT(*), ISNULL(MAX(CAST(IsAddedByHR AS INT)), 0) FROM Employees WHERE Email = @Email",
                        con, transaction))
                    {
                        empCheckCmd.Parameters.AddWithValue("@Email", email);
                        using (var reader = empCheckCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                empCount = reader.GetInt32(0);
                                isAddedByHR = reader.GetInt32(1) == 1;
                            }
                            else
                            {
                                empCount = 0;
                            }
                        }
                    }

                    if (empCount > 0 && isAddedByHR)
                    {
                        int reissueCount;
                        using (var reissueCheckCmd = new SqlCommand("SELECT COUNT(*) FROM ReissueRequests WHERE UserID = @UserID", con, transaction))
                        {
                            reissueCheckCmd.Parameters.AddWithValue("@UserID", newUserId);
                            reissueCount = (int)reissueCheckCmd.ExecuteScalar();
                        }

                        if (reissueCount == 0)
                        {
                            // ✅ Create Reissue Request
                            using (var insertReissueCmd = new SqlCommand(
                                @"INSERT INTO ReissueRequests (UserID, Role, RequestType, Reason) 
                                  VALUES (@UserID, 'Employee', 'New ID Card', 'New ID Card Request')", con, transaction))
                            {
                                insertReissueCmd.Parameters.AddWithValue("@UserID", newUserId);
                                insertReissueCmd.ExecuteNonQuery();
                            }

                            // ✅ Trigger Email to DTS
                            _emailSender.SendHRAddedEmployeeSignupNotification(email);
                        }
                    }
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public bool ValidateUser(string email, string password)
        {
            using var con = new SqlConnection(_connectionString);
            var cmd = new SqlCommand("SELECT PasswordHash FROM Users WHERE Email = @Email", con);
            cmd.Parameters.AddWithValue("@Email", email);
            con.Open();
            var result = cmd.ExecuteScalar();
            return result != null && result.ToString() == PasswordHasher.HashPassword(password);
        }

        public class UserModel
        {
            public int UserID { get; set; }
            public string Email { get; set; }
            public string Role { get; set; }
            public string DisplayName { get; set; } // ✅ Added for showing Student/Employee name
        }

        public UserModel GetUser(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                return null;

            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT UserID, Email, Role, PasswordHash FROM Users WHERE Email = @Email", con);

            cmd.Parameters.Add("@Email", System.Data.SqlDbType.NVarChar).Value = email;

            con.Open();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                string hash = reader["PasswordHash"].ToString();
                if (hash == PasswordHasher.HashPassword(password))
                {
                    var user = new UserModel
                    {
                        UserID = Convert.ToInt32(reader["UserID"]),
                        Email = reader["Email"].ToString(),
                        Role = reader["Role"].ToString(),
                        DisplayName = reader["Email"].ToString() // fallback to email
                    };

                    reader.Close(); // ✅ close reader before running another query

                    // ✅ Now get Student/Employee name using Email instead of UserID
                    if (user.Role == "Student")
                    {
                        using var studentCmd = new SqlCommand("SELECT FullName FROM Students WHERE Email = @Email", con);
                        studentCmd.Parameters.AddWithValue("@Email", user.Email);
                        var name = studentCmd.ExecuteScalar();
                        if (name != null) user.DisplayName = name.ToString();
                    }
                    else if (user.Role == "Employee")
                    {
                        using var empCmd = new SqlCommand("SELECT FullName FROM Employees WHERE Email = @Email", con);
                        empCmd.Parameters.AddWithValue("@Email", user.Email);
                        var name = empCmd.ExecuteScalar();
                        if (name != null) user.DisplayName = name.ToString();
                    }

                    return user;
                }
            }
            return null;
        }





            

    }
}
