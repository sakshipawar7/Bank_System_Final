using static BankSystem.Repo.AccountsRepo;
using BankSystem.Repo;

namespace BankSystem.Helpers
{
    public class Delegate
    {
        public delegate IAccountSavingsOrCurrent AccountDelegate(string type);
    }
}
