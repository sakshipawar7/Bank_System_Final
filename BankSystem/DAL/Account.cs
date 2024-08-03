namespace BankSystem.DAL
{
    public class Account
    {
        public long AccountNo { get; set; }
        public long CustomerId { get; set; }

        public string AccountType { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime AccountConvertedDate { get; set; } 
        public int RemainingWithdrawlAmountPerDay { get; set; }
        public int RemainingNoOfWithdrawlsPerDay { get; set; }
        public DateTime DateOfDeletion { get; set; }
        public DateTime LastResetDate { get; set; } = DateTime.Now;

        public bool IsDeleted { get; set; } = false;

        public Customer Customer { get; set; }
    }
}
