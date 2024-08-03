namespace BankSystem.DAL
{
    public class Transaction
    {
        public long TransactionId { get; set; }
        public long AccountId { get; set; }
        //public long ReceiverAccountId { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public DateTime TCreationDate { get; set; }
        
        public string Description { get; set; }
        public bool IsDeleted { get; set; } = false;

        public virtual Account Account { get; set; } // The account making the transaction
        /*public virtual Account ReceiverAccount { get; set; }*/ // The account receiving the transaction
    }
}
