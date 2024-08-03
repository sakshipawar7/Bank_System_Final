using AutoMapper;
using BankSystem.DAL;
using BankSystem.Helpers;
using BankSystem.Model;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using System.Globalization;
using System.Security.Cryptography;
using static BankSystem.Helpers.Delegate;

namespace BankSystem.Repo
{
    public interface IAccountsRepo 
    {
        Task<Result> GetAllAccounts(string accountType);
        Task<Result> AddAccount(AccountDTO accountDto);
        Task<Result> ConvertAccountTo(long accNo, string accountType);
        Task<Result> GetAllCustomersOfAccountType(string accountType);
        Task<Result> Transfer(long senderAccountId, long receiverAccountId, long amount);
        Task<Result> DepositOrWithdraw(long accountNo, long amount, string transactionType);

        Task<Result> GetAccountDetailsByAccountNo(long accountNo);
        Task<Result> GetAllAccountsOfTypeCreatedBetween(string accountType, DateTime startDate, DateTime endDate);
        Task<Result> GetAllCustomersOfAccountTypeCreatedBetween(string accountType, DateTime startDate, DateTime endDate);
        Task<Result> GetAllAccountsOfTypeConvertedBetween(string accountType, DateTime startDate, DateTime endDate);
        Task<Result> GetAllAccountsOfTypeHavingBalanceBetween(string accountType, decimal minBalance, decimal maxBalance);

       Result GetAccountTermsByAccountType(string accountType);

        Task<Result> DeleteAccount(long accountNo);



        //helper function
        Task<Result> BeforeTransferChecks(long senderAccountId, long receiverAccountId, long amount);

    }


    public class AccountsRepo : IAccountsRepo
    {
        private readonly BankDbContext _context;
        private readonly IMapper _mapper;
        private readonly AccountDelegate _delegate;
        private readonly ITransactionRepo _transactionRepo;

        public AccountsRepo(BankDbContext context, IMapper mapper, AccountDelegate delegates, ITransactionRepo transactionRepo)
        {
            _context = context;
            _mapper = mapper;
            _delegate = delegates;
            _transactionRepo = transactionRepo;
        }

        public async Task<Result> GetAllAccounts(string accountType)
        {
            List<Account> dalAccounts;

            if (string.Equals(accountType, "All", StringComparison.OrdinalIgnoreCase))
            {
                dalAccounts = await _context.Accounts.Where(a => !a.IsDeleted).ToListAsync();
            }
            else if (string.Equals(accountType, "Savings", StringComparison.OrdinalIgnoreCase))
            {
                dalAccounts = await _context.Accounts.Where(a => a.AccountType == "Savings" && !a.IsDeleted).ToListAsync();
            }
            else if (string.Equals(accountType, "Current", StringComparison.OrdinalIgnoreCase))
            {
                dalAccounts = await _context.Accounts.Where(a => a.AccountType == "Current" && !a.IsDeleted).ToListAsync();
            }
            else
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid account type specified\r\nValid values are 'Savings', 'Current' and 'All'."
                };
            }

            if (dalAccounts.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "No Accounts in database"
                };
            }

            var accounts = _mapper.Map<List<DisplayAccountsModel>>(dalAccounts);

            return new Result
            {
                Success = true,
                DisplayAccounts = accounts
            };
        }


        public async Task<Result> AddAccount(AccountDTO accountDto)
        {
            IAccountSavingsOrCurrent accountService;

            switch (accountDto.AccountType.ToLower())
            {
                case "savings":
                    accountService = _delegate("Savings");
                    break;
                case "current":
                    accountService = _delegate("Current");
                    break;
                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = "Invalid account type"
                    };
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == accountDto.CustomerId && !c.IsDeleted);
            if (customer == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Account creation failed\r\nCustomer should be created first."
                };
            }

            if (accountDto.Amount < accountService.MinBalance)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Minimum balance for savings account should be {accountService.MinBalance}"
                };
            }

            var account = new Account
            {
                CustomerId = accountDto.CustomerId,
                AccountType = accountDto.AccountType,
                Balance = accountDto.Amount,
                CreatedDate = DateTime.Now,
                AccountConvertedDate = DateTime.MinValue,
                RemainingWithdrawlAmountPerDay = accountService.MaxWithdrawlAmountPerDay,
                RemainingNoOfWithdrawlsPerDay = accountService.MaxNoOfWithdrawlsPerDay
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            var transactionResult = await _transactionRepo.AddTransaction(account.AccountNo, account.AccountNo, accountDto.Amount, "Deposit");
            if (!transactionResult.Success)
            {
                return transactionResult;
            }

            await _context.SaveChangesAsync();
            return new Result
            {
                Success = true,
                SuccessMsg = $"Account created successfully.\r\nYour account Id is {account.AccountNo}"
            };

        }



        public async Task<Result> ConvertAccountTo(long accNo, string accountType)
        {
            IAccountSavingsOrCurrent accountService;

            switch (accountType.ToLower())
            {
                case "savings":
                    accountService = _delegate("Savings");
                    break;
                case "current":
                    accountService = _delegate("Current");
                    break;
                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = "Invalid account type\r\nValid acoounts are 'Savings' and 'Current'."
                    };
            }

            if (accNo <= 222200000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Account Id cannot be 0 or negative"
                };
            }

            var initialAcc = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountNo == accNo && !a.IsDeleted);

            if (initialAcc == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No account found with account id {accNo}"
                };
            }

            if (initialAcc.AccountType == accountType)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Conversion failed.\r\nPrevious account type and this account type are same."
                };
            }

            if (initialAcc.Balance < accountService.MinBalance)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Conversion failed.\r\n" +
                    $"For converting account to Current Account the minimum balance should be {accountService.MinBalance}" +
                    $"\r\nPlease deposit {accountService.MinBalance - initialAcc.Balance} rupees and try again"
                };
            }

            initialAcc.AccountType = accountType;
            initialAcc.AccountConvertedDate = DateTime.Now;
            initialAcc.LastResetDate = DateTime.Now;
            initialAcc.RemainingNoOfWithdrawlsPerDay = accountService.MaxNoOfWithdrawlsPerDay;
            initialAcc.RemainingWithdrawlAmountPerDay = accountService.MaxWithdrawlAmountPerDay;
            _context.Accounts.Update(initialAcc);
            _context.SaveChanges();

            return new Result
            {
                Success = true,
                SuccessMsg = $"Account converted successfully to Account type {initialAcc.AccountType}"
            };

        }



        public async Task<Result> GetAllCustomersOfAccountType(string accountType)
        {

            List<Customer> dalCustomers;

            if (accountType.ToLower() == "All".ToLower())
            {
                dalCustomers = await _context.Accounts
                    .Where(a => !a.IsDeleted)
                    .Select(a => a.Customer)
                    .Distinct()
                    .Where(c => !c.IsDeleted)
                    .ToListAsync();
            }
            else if (string.Equals(accountType, "Savings", StringComparison.OrdinalIgnoreCase))
            {
                dalCustomers = await _context.Accounts
                    .Where(a => a.AccountType.ToLower() == "Savings".ToLower() && !a.IsDeleted)
                    .Select(a => a.Customer)
                    .Distinct()
                    .Where(c => !c.IsDeleted)
                    .ToListAsync();
            }
            else if (string.Equals(accountType, "Current", StringComparison.OrdinalIgnoreCase))
            {
                dalCustomers = await _context.Accounts
                    .Where(a => a.AccountType.ToLower() == "Current".ToLower() && !a.IsDeleted)
                    .Select(a => a.Customer)
                    .Distinct()
                    .Where(c => !c.IsDeleted)
                    .ToListAsync();
            }
            else
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid account type specified\r\nValid values are 'Savings', 'Current' and 'All'."
                };
            }

            if (dalCustomers.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No customer found of account type {accountType}"
                };
            }

            var customers = _mapper.Map<List<DisplayCustomerModel>>(dalCustomers);
            return new Result
            {
                Success = true,
                DisplayAllCustomers = customers
            };
        }


        public async Task<Result> Transfer(long senderAccountId, long receiverAccountId, long amount)
        {
            if (senderAccountId == receiverAccountId)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Sender and Receiver Id cannot be same"
                };
            }

            if (senderAccountId <= 222200000000 || receiverAccountId <= 222200000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid sender or receiver account ID."
                };
            }

            if (amount <= 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Amount cannot be negative or 0"
                };
            }


            var account = await _context.Accounts
                .Where(a => a.AccountNo == senderAccountId && !a.IsDeleted)
                .FirstOrDefaultAsync();
            if (account == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Sender Account Id Doesn't Exist"
                };
            }

            IAccountSavingsOrCurrent accountService;
            string? accountType = await _context.Accounts
                .Where(a => a.AccountNo == senderAccountId && !a.IsDeleted)
                .Select(a => a.AccountType)
                .FirstOrDefaultAsync();

            if(accountType == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid account id"
                };
            }

            switch (accountType.ToLower())
            {
                case "savings":
                    accountService = _delegate("Savings");
                    break;
                case "current":
                    accountService = _delegate("Current");
                    break;
                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = $"Sender account with account id {senderAccountId} doesn't exist"
                    };
            }

            var result = await BeforeTransferChecks(senderAccountId, receiverAccountId, amount);
            if (!result.Success)
            {
                return result;
            }
            var senderAccount = result.SenderAccount;
            var receiverAccount = result.ReceiverAccount;

            CheckLastResetAndUpdate(senderAccount, accountService);

            if (senderAccount.Balance - amount < accountService.MinBalance)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Transfer Rejected. \r\nWithdrawal would result in balance dropping below the minimum required balance of {AccountsRepo.ConvertDollarToRupeeSymbol(accountService.MinBalance)}."
                };
            }

            if (amount > senderAccount.RemainingWithdrawlAmountPerDay)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Transfer Rejected. \r\nExceeds daily withdrawal limit. You can withdraw only {ConvertDollarToRupeeSymbol(senderAccount.RemainingWithdrawlAmountPerDay)}"
                };
            }

            if (senderAccount.RemainingNoOfWithdrawlsPerDay <= 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Transfer Rejected. \r\nDaily withdrawal limit reached. You can withdraw money after {24 - (int)Math.Ceiling((DateTime.Now - senderAccount.LastResetDate).TotalHours)} hours."
                };
            }

            senderAccount.RemainingNoOfWithdrawlsPerDay -= 1;
            senderAccount.RemainingWithdrawlAmountPerDay -= (int)amount;

            senderAccount.Balance -= amount;
            receiverAccount.Balance += amount;

            var transactionResult = await _transactionRepo.AddTransaction(senderAccountId, receiverAccountId, amount, "Transfer");
            if (!transactionResult.Success)
            {
                return transactionResult;
            }

            _context.Accounts.Update(senderAccount);
            await _context.SaveChangesAsync();
            _context.Accounts.Update(receiverAccount);
            await _context.SaveChangesAsync();

            return new Result
            {
                Success = true,
                SuccessMsg = $"Transfer Successful.\r\nTransfer of {AccountsRepo.ConvertDollarToRupeeSymbol(amount)} from account {senderAccountId} to account {receiverAccountId} was successful.\r\n" +
                    $"Sender's remaining balance: {AccountsRepo.ConvertDollarToRupeeSymbol(senderAccount.Balance)}\r\n" +
                    $"Receiver's new balance: {AccountsRepo.ConvertDollarToRupeeSymbol(receiverAccount.Balance)}\r\n" +
                    $"Remaining number of withdrawals for the sender: {senderAccount.RemainingNoOfWithdrawlsPerDay}\r\n" +
                    $"Remaining withdrawal amount for the sender: {senderAccount.RemainingWithdrawlAmountPerDay:C}"
            };

        }




        public async Task<Result> BeforeTransferChecks(long senderAccountId, long receiverAccountId, long amount)
        {
            var senderAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountNo == senderAccountId && !a.IsDeleted);
            var receiverAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountNo == receiverAccountId && !a.IsDeleted);

            if (senderAccount == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Sender account with ID {senderAccountId} not found."
                };
            }

            if (receiverAccount == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Receiver account with ID {receiverAccountId} not found."
                };
            }

            if (senderAccount.Balance < amount)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Transfer Rejected. \r\nInsufficient balance. Current Balance is {AccountsRepo.ConvertDollarToRupeeSymbol(senderAccount.Balance)}."
                };
            }

            return new Result
            {
                Success = true,
                SenderAccount = senderAccount,
                ReceiverAccount = receiverAccount
            };
        }


        public void CheckLastResetAndUpdate(Account account, IAccountSavingsOrCurrent accountService)
        {
            if (DateTime.Now - account.LastResetDate > TimeSpan.FromDays(1))
            {
                account.RemainingWithdrawlAmountPerDay = accountService.MaxWithdrawlAmountPerDay;
                account.RemainingNoOfWithdrawlsPerDay = accountService.MaxNoOfWithdrawlsPerDay;
                account.LastResetDate = DateTime.Now;
            }
        }



        public async Task<Result> DepositOrWithdraw(long accountNo, long amount, string transactionType)
        {
            if (accountNo <= 222200000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Account Id cannot be {accountNo}"
                };
            }

            if (amount <= 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Amount cannot be negative or 0"
                };
            }

            IAccountSavingsOrCurrent accountService;
            string? accountType = await _context.Accounts
                .Where(a => a.AccountNo == accountNo && !a.IsDeleted)
                .Select(a => a.AccountType)
                .FirstOrDefaultAsync();


            if(accountType == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Invalid accout no {accountNo}"
                };
            }

            switch (accountType.ToLower())
            {
                case "savings":
                    accountService = _delegate("Savings");
                    break;
                case "current":
                    accountService = _delegate("Current");
                    break;
                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = $"Sender account with account id {accountNo} doesn't exist"
                    };
            }

            var dalAccount = await _context.Accounts.FindAsync(accountNo);
            if (dalAccount == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Account Id {accountNo} not found"
                };
            }

            if (transactionType.ToLower() == "deposit")
            {
                dalAccount.Balance += amount;
                var result = await _transactionRepo.AddTransaction(accountNo, accountNo, amount, "Deposit");
                if (!result.Success)
                {
                    return result;
                }
                return new Result
                {
                    Success = true,
                    SuccessMsg = $"Successfully deposited {AccountsRepo.ConvertDollarToRupeeSymbol(amount)} into account {accountNo}.\r\nNew balance is {AccountsRepo.ConvertDollarToRupeeSymbol(dalAccount.Balance)}."
                };
            }
            else if (transactionType.ToLower() == "withdraw")
            {
                if (dalAccount.Balance - amount < accountService.MinBalance)
                {
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = $"Withdraw rejected\r\nWithdrawal would result in balance dropping below the minimum required balance of {AccountsRepo.ConvertDollarToRupeeSymbol(accountService.MinBalance)}."
                    };
                }

                CheckLastResetAndUpdate(dalAccount, accountService);

                if (amount > dalAccount.RemainingWithdrawlAmountPerDay)
                {
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = $"Withdrawal Rejected. \r\nExceeds daily withdrawal limit.\r\nYou can withdraw only {AccountsRepo.ConvertDollarToRupeeSymbol(dalAccount.RemainingWithdrawlAmountPerDay)}"
                    };
                }

                if (dalAccount.RemainingNoOfWithdrawlsPerDay <= 0)
                {
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = $"Withdrawal Rejected. \r\nDaily withdrawal limit reached. You can withdraw money after {24 - (int)Math.Ceiling((DateTime.Now - dalAccount.LastResetDate).TotalHours)} hours."
                    };
                }

                dalAccount.RemainingNoOfWithdrawlsPerDay -= 1;
                dalAccount.RemainingWithdrawlAmountPerDay -= (int)amount;

                dalAccount.Balance -= amount;

                var result = await _transactionRepo.AddTransaction(accountNo, accountNo, amount, "Withdraw");
                if (!result.Success)
                {
                    return result;
                }

                return new Result
                {
                    Success = true,
                    SuccessMsg = $"Successfully withdrawed {AccountsRepo.ConvertDollarToRupeeSymbol(amount)} from account {accountNo}." +
                    $"\r\nNew balance is {AccountsRepo.ConvertDollarToRupeeSymbol(dalAccount.Balance)}."+
                    $"Remaining number of withdrawals for the sender: {dalAccount.RemainingNoOfWithdrawlsPerDay}\r\n" +
                    $"Remaining withdrawal amount for the sender: {dalAccount.RemainingWithdrawlAmountPerDay:C}"
                };
            }

            return new Result
            {
                Success = true
            };

        }



        public async Task<Result> GetAccountDetailsByAccountNo(long accountNo)
        {
            if (accountNo <= 222200000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid Account no.\r\nAccount no starts from 222200000001"
                };
            }

            var dalAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountNo == accountNo && !a.IsDeleted);

            if (dalAccount == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No account found with account no {accountNo}"
                };
            }

            var account = _mapper.Map<DisplayAccountsModel>(dalAccount);
            return new Result
            {
                Success = true,
                DisplayAccount = account
            };
        }



        public async Task<Result> GetAllAccountsOfTypeCreatedBetween(string accountType, DateTime startDate, DateTime endDate)
        {
            var result = ValidateStartAndEndDate(startDate, endDate);
            if (!result.Success)
            {
                return result;
            }

            List<Account> dalAccounts;
            string accType = accountType.ToLower();

            if (accType == "all")
            {
                dalAccounts = await _context.Accounts.Where(a => a.CreatedDate >= startDate && a.CreatedDate <= endDate && !a.IsDeleted).ToListAsync();
            }
            else if (accType == "savings")
            {
                dalAccounts = await _context.Accounts.Where(a => a.AccountType.ToLower() == "savings" && a.CreatedDate >= startDate && a.CreatedDate <= endDate && !a.IsDeleted).ToListAsync();
            }
            else if (accType == "current")
            {
                dalAccounts = await _context.Accounts.Where(a => a.AccountType.ToLower() == "current" && a.CreatedDate >= startDate && a.CreatedDate <= endDate && !a.IsDeleted).ToListAsync();
            }
            else
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid account type specified.\r\nValid account types are 'Savings', 'Current' and 'All'."
                };
            }

            result = CheckCount(dalAccounts, startDate, endDate);
            if (!result.Success)
            {
                return result;
            }

            var accounts = _mapper.Map<List<DisplayAccountsModel>>(dalAccounts);
            return new Result
            {
                Success = true,
                DisplayAccounts = accounts
            };
        }


        public async Task<Result> GetAllCustomersOfAccountTypeCreatedBetween(string accountType, DateTime startDate, DateTime endDate)
        {
            var result = ValidateStartAndEndDate(startDate, endDate);
            if (!result.Success)
            {
                return result;
            }

            List<Customer> dalCustomers;
            string accType = accountType.ToLower();

            if (accType == "all")
            {
                dalCustomers = await _context.Customers
                    .Include(c => c.Accounts)
                    .Where(c => c.Accounts.Any(a => a.CreatedDate >= startDate && a.CreatedDate <= endDate && !a.IsDeleted))
                    .ToListAsync();
            }
            else if (accType == "savings" || accType == "current")
            {
                dalCustomers = await _context.Customers
                    .Include(c => c.Accounts)
                    .Where(c => c.Accounts.Any(a => a.AccountType.ToLower() == accType && a.CreatedDate >= startDate && a.CreatedDate <= endDate && !a.IsDeleted))
                    .ToListAsync();
            }
            else
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid account type specified.\r\nValid account types are 'Savings', 'Current' and 'All'."
                };
            }

            if (dalCustomers.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No customers found created betwwen daterange {startDate.ToString("yyyy-MM-dd HH:mm:ss")} and {endDate.ToString("yyyy-MM-dd HH:mm:ss")}"
                };
            }

            var customers = _mapper.Map<List<DisplayCustomerModel>>(dalCustomers);
            return new Result
            {
                Success = true,
                DisplayAllCustomers = customers
            };
        }


        public async Task<Result> GetAllAccountsOfTypeConvertedBetween(string accountType, DateTime startDate, DateTime endDate)
        {
            var result = ValidateStartAndEndDate(startDate, endDate);
            if (!result.Success)
            {
                return result;
            }

            List<Account> dalAccounts;
            string accType = accountType.ToLower();

            if (accType == "all")
            {
                dalAccounts = await _context.Accounts.Where(a => a.AccountConvertedDate >= startDate && a.AccountConvertedDate <= endDate && !a.IsDeleted).ToListAsync();
            }
            else if (accType == "savings")
            {
                dalAccounts = await _context.Accounts.Where(a => a.AccountType.ToLower() == "savings" && a.AccountConvertedDate >= startDate && a.AccountConvertedDate <= endDate && !a.IsDeleted).ToListAsync();
            }
            else if (accType == "current")
            {
                dalAccounts = await _context.Accounts.Where(a => a.AccountType.ToLower() == "current" && a.AccountConvertedDate >= startDate && a.AccountConvertedDate <= endDate && !a.IsDeleted).ToListAsync();
            }
            else
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid account type specified.\r\nValid account types are 'Savings', 'Current' and 'All'."
                };
            }

            result = CheckCount(dalAccounts, startDate, endDate);
            if (!result.Success)
            {
                return result;
            }

            var accounts = _mapper.Map<List<DisplayAccountsModel>>(dalAccounts);
            return new Result
            {
                Success = true,
                DisplayAccounts = accounts
            };
        }


        public async Task<Result> GetAllAccountsOfTypeHavingBalanceBetween(string accountType, decimal minBalance, decimal maxBalance)
        {
            var result = ValidateMinAndMaxBalancee(minBalance, maxBalance);
            if (!result.Success)
            {
                return result;
            }

            List<Account> dalAccounts;
            string accType = accountType.ToLower();

            if (accType == "all")
            {
                dalAccounts = await _context.Accounts.Where(a => a.Balance >= minBalance && a.Balance <= maxBalance && !a.IsDeleted).ToListAsync();
            }
            else if (accType == "savings")
            {
                dalAccounts = await _context.Accounts.Where(a => a.AccountType.ToLower() == "savings" && a.Balance >= minBalance && a.Balance <= maxBalance && !a.IsDeleted).ToListAsync();
            }
            else if (accType == "current")
            {
                dalAccounts = await _context.Accounts.Where(a => a.AccountType.ToLower() == "current" && a.Balance >= minBalance && a.Balance <= maxBalance && !a.IsDeleted).ToListAsync();
            }
            else
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid account type specified.\r\nValid account types are 'Savings', 'Current' and 'All'."
                };
            }

            if (dalAccounts.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No accounts found betwwen range {AccountsRepo.ConvertDollarToRupeeSymbol(minBalance)} - {AccountsRepo.ConvertDollarToRupeeSymbol(maxBalance)}"
                };
            }

            var accounts = _mapper.Map<List<DisplayAccountsModel>>(dalAccounts);
            return new Result
            {
                Success = true,
                DisplayAccounts = accounts
            };
        }



        public Result ValidateStartAndEndDate(DateTime startDate, DateTime endDate)
        {
            if (startDate > DateTime.Now)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Start date should be before date-time {DateTime.Now}"
                };
            }

            if (startDate > endDate)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Start date should be before End date"
                };
            }

            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "No accounts found"
                };
            }
            return new Result
            {
                Success = true
            };
        }



        public Result ValidateMinAndMaxBalancee(decimal minBalance, decimal maxBalance)
        {

            if (minBalance > maxBalance)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Minimum balance should be less than maximum balance"
                };
            }

            if (minBalance < 0 || maxBalance < 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Balance cannot be negative"
                };
            }
            return new Result
            {
                Success = true
            };
        }


        public Result CheckCount(List<Account> dalAccounts, DateTime startDate, DateTime endDate)
        {
            if (dalAccounts.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No accounts found betwwen daterange {startDate.ToString("yyyy-MM-dd HH:mm:ss")} and {endDate.ToString("yyyy-MM-dd HH:mm:ss")}"
                };
            }
            return new Result
            {
                Success = true
            };
        }





        public async Task<Result> DeleteAccount(long accountNo)
        {
            if (accountNo <= 222200000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid Account no.\r\nAccount no starts from 222200000001"
                };
            }

            var account = await _context.Accounts.FirstOrDefaultAsync(x => x.AccountNo == accountNo && !x.IsDeleted);
            if (account == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "No account found"
                };
            }


            var customer = await _context.Customers
                .Include(c => c.Accounts)
                .FirstOrDefaultAsync(c => c.CustomerId == account.CustomerId && !c.IsDeleted);

            if (customer == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Customer not found"
                };
            }


            int accountCount = await _context.Accounts
                .CountAsync(a => a.CustomerId == customer.CustomerId && !a.IsDeleted);

            account.IsDeleted = true;
            account.DateOfDeletion = DateTime.Now;
            _context.SaveChanges();


            var transactions = await _context.Transactions
                .Where(t => (t.AccountId == accountNo)
                && (t.TransactionType == "Withdraw" || t.TransactionType == "Deposit")
                && !t.IsDeleted)
                .ToListAsync();

            foreach (var transaction in transactions)
            {
                transaction.IsDeleted = true;
                _context.SaveChanges();
            }

            string successMsg = $"Account with account no {accountNo} deleted successfully.";
            if (accountCount <= 1)
            {
                successMsg = $"Account {accountNo} deleted successfully. Customer {customer.CustomerId} also deleted, as it was their last account.";
                customer.IsDeleted = true;
                _context.SaveChanges();

            }

            return new Result
            {
                Success = true,
                SuccessMsg = successMsg
            };
        }



        public Result GetAccountTermsByAccountType(string accountType)
        {
            IAccountSavingsOrCurrent accountService;
            switch (accountType.ToLower())
            {
                case "savings":
                    accountService = _delegate("Savings");
                    break;
                case "current":
                    accountService = _delegate("Current");
                    break;
                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = "Invalid ccount type.\r\nValid values account types are 'Savings' and 'Current'."
                    };

            }
            return new Result
            {
                Success = true,
                Data =
                new
                {
                    Min_Balance = accountService.MinBalance,
                    Interest_Rate = accountService.InterestRate,
                    MaxWithdrawl_AmountPerDay = accountService.MaxWithdrawlAmountPerDay,
                    MaxNoOfWithdrawls_PerDay = accountService.MaxNoOfWithdrawlsPerDay
                }
            };
        }


        //helper functions
        public static string ConvertDollarToRupeeSymbol(decimal amount)
        {
            string formattedAmount = amount.ToString("C", new CultureInfo("en-IN"));
            return formattedAmount;
        }

    }



    public interface IAccountSavingsOrCurrent
    {
        public int MinBalance { get; set; }
        public decimal InterestRate { get; set; }
        public int MaxWithdrawlAmountPerDay { get; set; }
        public int MaxNoOfWithdrawlsPerDay { get; set; }
    }



    public class Savings : IAccountSavingsOrCurrent
    {
        public int MinBalance { get; set; } = 10_000;
        public decimal InterestRate { get; set; } = 3.5m;
        public int MaxWithdrawlAmountPerDay { get; set; } = 25_000;
        public int MaxNoOfWithdrawlsPerDay { get; set; } = 5;
                        
    }



    public class Current : IAccountSavingsOrCurrent
    {
        public int MinBalance { get; set; } = 5000;
        public decimal InterestRate { get; set; } = 1.1m;
        public int MaxWithdrawlAmountPerDay { get; set; } = 1_00_00_000;
        public int MaxNoOfWithdrawlsPerDay { get; set; } = 6;
                
    }
}
