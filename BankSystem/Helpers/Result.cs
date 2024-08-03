using BankSystem.DAL;
using BankSystem.Model;

namespace BankSystem.Helpers
{
    public class Result
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public object Data { get; set; }
        public IEnumerable<string> ValidationErrors { get; set; }

        public DisplayCustomerModel DisplayCustomer { get; set; }
        public IEnumerable<DisplayCustomerModel> DisplayAllCustomers { get; set; }
        public IEnumerable<DisplayAccountsModel> DisplayAccounts { get; set; }
        public DisplayAccountsModel DisplayAccount  { get; set; }

        public string SuccessMsg { get; set; }

        public Account SenderAccount { get; set; }
        public Account ReceiverAccount { get; set; }

        public IEnumerable<TransactionModel> Transactions { get; set; }
        public TransactionModel Transaction { get; set; }
    }
}
