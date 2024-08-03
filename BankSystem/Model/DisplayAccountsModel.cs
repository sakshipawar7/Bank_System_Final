namespace BankSystem.Model
{
    public class DisplayAccountsModel
    {
        public long CustomerId { get; set; }
        public long AccountNo { get; set; }
        public string AccountType { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime AccountConvertedDate { get; set; }
        public int RemainingWithdrawlAmountPerDay { get; set; }
        public int RemainingNoOfWithdrawlsPerDay { get; set; }
        public DateTime DateOfDeletion { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
