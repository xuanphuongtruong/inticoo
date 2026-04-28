using System.ComponentModel.DataAnnotations;

namespace InticooInspection.Domain.Entities
{
    public class MailLog
    {
        public int       Id              { get; set; }
        public int?      VendorId        { get; set; }

        [MaxLength(50)]
        public string?   VendorCode      { get; set; }

        [Required, MaxLength(200)]
        public string    ToEmail         { get; set; } = "";

        [Required, MaxLength(300)]
        public string    Subject         { get; set; } = "";

        public DateTime  SentAt          { get; set; }
        public bool      IsSuccess       { get; set; }

        [MaxLength(2000)]
        public string?   ErrorMessage    { get; set; }

        public int       InspectionCount { get; set; }
    }
}
