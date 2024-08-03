using System.ComponentModel.DataAnnotations;

namespace BankSystem.Model
{
    public class UpdateCustomerModel
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
    }
}
