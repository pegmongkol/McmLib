namespace McmLib.Models
{
    public class ApiResponse
    {
        public bool Result { get; set; }
        public string? Message { get; set; }
        public string? Endpoint { get; set; }
        public int? Status { get; set; }
        public long? ElapsedMs { get; set; }
        public bool IsCanceled { get; set; }
        public string? ContentType { get; set; }
        public string? BodyPrefix { get; set; }
    }

    public class LoginApiResponse : ApiResponse
    {
        public int? User_Id { get; set; }
        public string? Username { get; set; }
        public string? Token { get; set; }
        public int FailedAttempts { get; set; }
        public int? Remaining_Attempts { get; set; }
        public bool Email_Notified { get; set; }
        public string? Locked_Until { get; set; }
    }

    public class ChangePasswordApiResponse : ApiResponse
    {
        public bool Check_Password { get; set; }
        public bool Change_Password { get; set; }
    }

    public class CompaniesApiResponse : ApiResponse
    {
        public List<Company> Companies { get; set; } = new List<Company>();
    }

    public class BranchesApiResponse : ApiResponse
    {
        public List<Branch> Branches { get; set; } = new List<Branch>();
    }

    public class LoginAlertApiResponse : ApiResponse
    {
        public bool EmailSent { get; set; }
        public int FailedAttempts { get; set; }
        public string? Email { get; set; }
        public bool Email_Notified { get; set; }
        public string? Locked_Until { get; set; }
        public int? Remaining_Attempts { get; set; }
        public string? DeliveryMethod { get; set; }
    }
}
