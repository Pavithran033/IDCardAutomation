using System.Net;
using System.Net.Mail;

namespace IDCardAutomation.Utils
{
    public class EmailSender
    {
        private readonly IConfiguration _config;

        public EmailSender(IConfiguration config)
        {
            _config = config;
        }
        //resent password method
        public void SendResetLink(string recipientEmail, string resetLink)
        {
            var settings = _config.GetSection("EmailSettings");

            var smtpClient = new SmtpClient(settings["SmtpServer"])
            {
                Port = int.Parse(settings["SmtpPort"]),
                Credentials = new NetworkCredential(settings["SenderEmail"], settings["SenderPassword"]),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings["SenderEmail"]),
                Subject = "Password Reset Request",
                Body = $"Click the following link to reset your password:\n\n{resetLink}",
                IsBodyHtml = false
            };




            mailMessage.To.Add(recipientEmail);

            smtpClient.Send(mailMessage); // ✅ This is where the email is actually sent
        }


       


        // reissue form trigger email method
        public void SendNewRequestNotification(string submittedByRole)
        {
            var settings = _config.GetSection("EmailSettings");

            var smtpClient = new SmtpClient(settings["SmtpServer"])
            {
                Port = int.Parse(settings["SmtpPort"]),
                EnableSsl = true,
                Credentials = new NetworkCredential(settings["SenderEmail"], settings["SenderPassword"])
            };

            string toEmail = submittedByRole == "Student"
                ? settings["OsaEmail"]   // For students, send to OSA
                : settings["DtsEmail"];  // For employees, send to DTS

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings["SenderEmail"]),
                Subject = "New ID Card Reissue Request Submitted",
                Body = $"A new reissue request has been submitted by a {submittedByRole.ToLower()}. Please review it in the system.",
                IsBodyHtml = false
            };

            mailMessage.To.Add(toEmail);

            smtpClient.Send(mailMessage);
        }


      
        // When OSA approves the student reissue request — notify DTS
        public void SendOsaApprovalToDTS(string studentName, string rollNumber)
        {
            var settings = _config.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(settings["SmtpServer"])
            {
                Port = int.Parse(settings["SmtpPort"]),
                EnableSsl = true,
                Credentials = new NetworkCredential(settings["SenderEmail"], settings["SenderPassword"])
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings["SenderEmail"]),
                To = { settings["DtsEmail"] },
                Subject = "Student ID Reissue Request Approved by OSA",
                Body = $@"A student's ID reissue request has been approved by OSA.

Student Name: {studentName}
Roll Number: {rollNumber}

Please log in to review and process the request.

Regards,
OSA Portal",
                IsBodyHtml = false
            };

            smtpClient.Send(mailMessage);
        }

        // When OSA rejects a student request — notify the student
        public void SendOsaRejectionToStudent(string studentEmail, string studentName, string remarks)
        {
            var settings = _config.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(settings["SmtpServer"])
            {
                Port = int.Parse(settings["SmtpPort"]),
                EnableSsl = true,
                Credentials = new NetworkCredential(settings["SenderEmail"], settings["SenderPassword"])
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings["SenderEmail"]),
                To = { studentEmail },
                Subject = "Your ID Card Reissue Request was Rejected",
                Body = $@"Dear {studentName},

We regret to inform you that your ID card reissue request has been rejected by the OSA team.

Remarks: {remarks}

You may contact OSA for further clarification if needed.

Regards,
OSA Office",
                IsBodyHtml = false
            };

            

            smtpClient.Send(mailMessage);
        }
// admin to studet delivery mail
        public void SendDeliveryReadyToStudent(string toEmail, string fullName)
        {
            var s = _config.GetSection("EmailSettings");
            using var client = new SmtpClient(s["SmtpServer"])
            {
                Port = int.Parse(s["SmtpPort"]),
                EnableSsl = true,
                Credentials = new NetworkCredential(s["SenderEmail"], s["SenderPassword"])
            };
            var mail = new MailMessage
            {
                From = new MailAddress(s["SenderEmail"]),
                Subject = "Your ID Card Is Ready for Collection",
                Body = $"Dear {fullName},\n\n" +
                       "Your new ID card is now ready. You can collect it from K-Mart during office hours.\n\n" +
                       "Regards,\nDTS Desk",
                IsBodyHtml = false
            };
            mail.To.Add(toEmail);
            client.Send(mail);
        }




        // hr to dts mail
        // When employee added by HR signs up for the first time — notify DTS
        public void SendHRAddedEmployeeSignupNotification(string employeeEmail)
        {
            var settings = _config.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(settings["SmtpServer"])
            {
                Port = int.Parse(settings["SmtpPort"]),
                EnableSsl = true,
                Credentials = new NetworkCredential(settings["SenderEmail"], settings["SenderPassword"])
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings["SenderEmail"]),
                To = { settings["DtsEmail"] }, // DTS gets the email
                Subject = "New Employee Signed Up (Added by HR)",
                Body = $@"An employee who was added by HR has signed up for the first time.

Email: {employeeEmail}

Please log in to the admin panel to review their details or process related actions.

Regards,
ID Card Automation System",
                IsBodyHtml = false
            };

            smtpClient.Send(mailMessage);
        }





        //admin delivery mail to employee
        // Notify employee when their ID card is marked as delivered
        public void SendDeliveryNotificationToEmployee(string toEmail, string fullName)
        {
            var settings = _config.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(settings["SmtpServer"])
            {
                Port = int.Parse(settings["SmtpPort"]),
                EnableSsl = true,
                Credentials = new NetworkCredential(settings["SenderEmail"], settings["SenderPassword"])
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings["SenderEmail"]),
                To = { toEmail },
                Subject = "Your ID Card Has Been Delivered",
                Body = $@"Dear {fullName},

Your ID card has been marked as delivered.

Please collect it from the DTS Desk at your earliest convenience.

Regards,
DTS Desk",
                IsBodyHtml = false
            };

            smtpClient.Send(mailMessage);
        }



        // When DTS rejects a reissue request — notify the employee
        public void SendRejectionNotificationToEmployee(string employeeEmail, string employeeName, string remarks)
        {
            var settings = _config.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(settings["SmtpServer"])
            {
                Port = int.Parse(settings["SmtpPort"]),
                EnableSsl = true,
                Credentials = new NetworkCredential(settings["SenderEmail"], settings["SenderPassword"])
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings["SenderEmail"]),
                To = { employeeEmail },
                Subject = "Your ID Card Reissue Request was Rejected",
                Body = $@"Dear {employeeName},

Your ID card reissue request has been rejected by the DTS team.

Remarks: {remarks}

If you believe this was a mistake or need clarification, please contact DTS directly.

Regards,
ID Card Automation System",
                IsBodyHtml = false
            };

            smtpClient.Send(mailMessage);
        }


        //clearance form email notification
        public void Send(string to, string subject, string body)
        {
            string fromEmail = _config["EmailSettings:SenderEmail"];
            string password = _config["EmailSettings:SenderPassword"];
            string smtpHost = _config["EmailSettings:SmtpServer"];
            int smtpPort = int.Parse(_config["EmailSettings:SmtpPort"]);
            string senderName = _config["EmailSettings:SenderName"] ?? "No Reply";

            using var smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(fromEmail, password),
                EnableSsl = true,
                UseDefaultCredentials = false
            };

            var mailMessage = new MailMessage()
            {
                From = new MailAddress(fromEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(to);
            smtpClient.Send(mailMessage);
        }

        // Notify Employee when all clearances are approved
        public void SendClearanceApprovedNotification(string employeeEmail, string employeeName)
        {
            var settings = _config.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(settings["SmtpServer"])
            {
                Port = int.Parse(settings["SmtpPort"]),
                EnableSsl = true,
                Credentials = new NetworkCredential(settings["SenderEmail"], settings["SenderPassword"])
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings["SenderEmail"]),
                To = { employeeEmail },
                Subject = "Clearance Request Approved",
                Body = $@"Dear {employeeName},

We are pleased to inform you that your employee clearance form has been approved by all departments.

You are now fully cleared in the system. Please contact HR for any final formalities if needed.

Regards,  
Employee Clearance System",
                IsBodyHtml = false
            };

            smtpClient.Send(mailMessage);
        }


        // HR updated resignation date → notify Employee
        public void SendResignationUpdatedToEmployee(string employeeEmail, string employeeName, DateTime resignationDate)
        {
            var settings = _config.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(settings["SmtpServer"])
            {
                Port = int.Parse(settings["SmtpPort"]),
                EnableSsl = true,
                Credentials = new NetworkCredential(settings["SenderEmail"], settings["SenderPassword"])
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings["SenderEmail"]),
                To = { employeeEmail },
                Subject = "Your Resignation Date Has Been Updated",
                Body = $@"Dear {employeeName},

HR has updated your resignation date in the system to {resignationDate:dd-MMM-yyyy}.

You can now log in to the Employee Clearance Portal and track your clearance status.

Regards,
HR Department",
                IsBodyHtml = false
            };

            smtpClient.Send(mailMessage);
        }



    }
}

