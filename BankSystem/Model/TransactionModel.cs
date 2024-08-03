namespace BankSystem.Model
{
    public class TransactionModel
    {
        public long AccountId { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public DateTime TCreationDate { get; set; }
        public string Description { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
