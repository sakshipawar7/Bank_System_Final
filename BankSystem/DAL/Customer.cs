using System.ComponentModel.DataAnnotations;

namespace BankSystem.DAL
{
    public class Customer
    {
        public long CustomerId { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Surname { get; set; }

        [Phone]
        public string Mobile { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Gender { get; set; }
        [Required]
        public DateTime DateOfBirth { get; set; }
        [Required]
        public string PanNo { get; set; }
        [Required]
        public string AadharNo { get; set; }
        [Required]
        public string BranchLocation { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime CustomerDetailsUpdateDate { get; set; } = DateTime.MinValue;
        public DateTime CustomerDeletionDate { get; set; } = DateTime.MinValue;

        public ICollection<Account> Accounts { get; set; } = new List<Account>();
    }
}
