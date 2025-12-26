namespace IDCardAutomation.Models
{
    public class ReissueRequestViewModel
    {
        public int RequestID { get; set; }

        // Common fields
        public string RequestType { get; set; }
        public string Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ApprovalStatus { get; set; }
        public string DeliveryStatus { get; set; }
        public int UserID { get; set; }

        // For students
        public int? StudentID { get; set; }
        public string? RollNumber { get; set; }
        public string? FullName { get; set; }

        // For employees
        public int? EmployeeID { get; set; }
        public string? EmpCode { get; set; }
        public string? EmpFullName { get; set; }

        public bool IsEmployee => EmployeeID.HasValue;


        // for reissue requests status page
        public string EntityType { get; set; } // "Student" or "Employee"
        public string EntityID { get; set; }
         public string CodeOrRoll { get; set; } // Roll number for students, Employee code for employees



        public string OSAStatus { get; set; }
        public string DTSStatus { get; set; }
      
        public int RequestCount { get; set; }
        public string CurrentStatus { get; set; }
        public string Remarks { get; set; }

        public DateTime DeliveryTime { get; set; }

        public string DTS_Remarks { get; set; }

        public string Email { get; set; }



    }


}




