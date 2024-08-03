using AutoMapper;
using BankSystem.DAL;
using BankSystem.Helpers;
using BankSystem.Model;
using Microsoft.EntityFrameworkCore; 
using System;
using System.Threading.Tasks; 
using static BankSystem.Helpers.Delegate;

namespace BankSystem.Repo
{
    public interface ITransactionRepo
    {
        Task<Result> AddTransaction(long sAccId,long rAccId, decimal amount, string transactionType);
        Task<Result> GetTransactionSummaryOfAccountNo(long accountNo, string transactionType);
        Task<Result> GetTransactionByTransactionId(long transactionId);
        Task<Result> GetAllTransactionsByCustomerId(long customerId, string transactionType);
        Task<Result> GetAllDeletedTransactionsOfTransactionTypeByCustomerId(string transactionType, long customerId);
        Task<Result> GetAllTransactionsBetweenDateRangeOfType(string transactionType, DateTime startDate, DateTime endDate);
        Task<Result> GetTransactionsBetweenAmountRangeByTransactionType(string transactionType, decimal minAmount, decimal maxAmount);
        Task<Result> GetTransactionsMadeTo(long receiverAccountId);
        Task<Result> RevertTransaction(long transactionId);

    }

    public class TransactionRepo : ITransactionRepo
    {
        private readonly BankDbContext _context;
        private readonly IMapper _mapper;
        private readonly AccountDelegate _delegate;

        public TransactionRepo(BankDbContext context, IMapper mapper, AccountDelegate @delegate)
        {
            _context = context;
            _mapper = mapper;
            _delegate = @delegate;
         }


        private long TransactionIdGenerator()
        {
            var maxTransactionId = _context.Transactions
                .OrderByDescending(t => t.TransactionId)
                .Select(t => t.TransactionId)
                .FirstOrDefault();

            if (maxTransactionId == 0)
            {
                return 111100000001;
            }

            return maxTransactionId + 1;
        }


        public async Task<Result> AddTransaction(long accId, long rAccId, decimal amount, string transactionType)
        {
            var dalAccount = await _context.Accounts.FindAsync(accId);
            if (dalAccount == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Account with account id {accId} not found"
                };
            }

            string description;
            switch (transactionType)
            {
                case "Deposit":
                    description = $"Account Id {accId} credited with {AccountsRepo.ConvertDollarToRupeeSymbol(amount)}";
                    break;

                case "Withdrawal":
                    description = $"Account Id {accId} debited with {AccountsRepo.ConvertDollarToRupeeSymbol(amount)}";
                    break;

                default:
                    description = $"Transaction of type {transactionType} occurred.";
                    break;
            }
                      

            if (transactionType.ToLower() == "transfer")
            {
                var transactionId = TransactionIdGenerator();

                var senderTransaction = new Transaction
                {
                    TransactionId = transactionId,
                    AccountId = accId,
                    TransactionType = "Withdraw",
                    Amount = amount,
                    TCreationDate = DateTime.Now,
                    Description = $"{AccountsRepo.ConvertDollarToRupeeSymbol(amount)} debited.\r\nTransferred to account id : {rAccId}"
                };

                _context.Transactions.Add(senderTransaction);
                await _context.SaveChangesAsync();


                var receiverTransaction = new Transaction
                {
                    TransactionId= transactionId,
                    AccountId = rAccId,
                    TransactionType = "Deposit",
                    Amount = amount,
                    TCreationDate = DateTime.Now,
                    Description = $"{AccountsRepo.ConvertDollarToRupeeSymbol(amount)} credited.\r\nReceived from account id: {accId}"
                };


                _context.Transactions.Add(receiverTransaction);
                await _context.SaveChangesAsync();
            }


            if(transactionType.ToLower() == "deposit" || transactionType.ToLower() == "withdraw")
            {
                var transactionId = TransactionIdGenerator();
                var transaction = new Transaction
                {
                    TransactionId = transactionId,
                    AccountId = accId,
                    TransactionType = transactionType,
                    Amount = amount,
                    TCreationDate = DateTime.Now,
                    Description = description
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();
            }                        

            return new Result
            {
                Success = true
            };
        }


        public async Task<Result> RevertTransaction(long transactionId)
        {
            if (transactionId <= 111100000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid Transaction Id\r\nTransaction Id start from 111100000001"
                };
            }

            var transactions = await _context.Transactions.Where(t => t.TransactionId == transactionId && !t.IsDeleted).ToListAsync();
            if (transactions == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Transaction doesn't exist."
                };
            }

            if(transactions.Count != 2)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Transaction is not of type transfer.\r\nCannot revert."
                };
            }

            long senderAccountId = transactions[0].AccountId;
            long receiverAccountId = transactions[1].AccountId;

            //revert
            var result = await Transfer(receiverAccountId, senderAccountId, (long)transactions[0].Amount);
            if (!result.Success)
            {
                return result;
            }
            return new Result
            {
                Success = true,
                SuccessMsg = $"Transaction reverted.\r\n{AccountsRepo.ConvertDollarToRupeeSymbol(transactions[0].Amount)} reverted from Account Id : {receiverAccountId} to Account Id : {senderAccountId}"
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

            if (accountType == null)
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
                    ErrorMessage = $"Transfer Rejected. \r\nExceeds daily withdrawal limit. You can withdraw only {AccountsRepo.ConvertDollarToRupeeSymbol(senderAccount.RemainingWithdrawlAmountPerDay)}"
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

            var transactionResult = await AddTransaction(senderAccountId, receiverAccountId, amount, "Transfer");
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

        

        public async Task<Result> GetTransactionSummaryOfAccountNo(long accountNo, string transactionType)
        {
            if (accountNo <= 222200000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid Account no.\r\nAccount no starts from 222200000001"
                };
            }


            List<Transaction> dalTransactions = new List<Transaction>();
            switch (transactionType.ToLower())
            {
                case "deposit":
                    dalTransactions = await _context.Transactions
                .Where(t => t.AccountId == accountNo && t.TransactionType.ToLower() == "deposit" && !t.IsDeleted)
                .ToListAsync();
                    break;

                case "withdraw":
                    dalTransactions = await _context.Transactions
                .Where(t => t.AccountId == accountNo && t.TransactionType.ToLower() == "withdraw" && !t.IsDeleted)
                .ToListAsync();
                    break;

                case "all":
                    dalTransactions = await _context.Transactions
                .Where(t => t.AccountId == accountNo && !t.IsDeleted)
                .ToListAsync();
                    break;

                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = "Invalid account type.\r\nValid values are 'Deposit', 'Withdraw', 'Transfer' and 'All'."
                    };
            }

            if (dalTransactions.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "No transactions found"
                };
            }

            var transactions = _mapper.Map<List<TransactionModel>>(dalTransactions);

            if (transactions.Any())
            {
                return new Result
                {
                    Success = true,
                    Transactions = transactions
                };
            }

            return new Result
            {
                Success = false,
                ErrorMessage = "No transactions found"
            };
        }



        public async Task<Result> GetTransactionByTransactionId(long transactionId)
        {
            if (transactionId <= 111100000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid Transaction Id\r\nTransaction Id start from 111100000001"
                };
            }

            var dalTransaction = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId && !t.IsDeleted);
            if (dalTransaction == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Transaction doesn't exist."
                };
            }

            var transaction = _mapper.Map<TransactionModel>(dalTransaction);

            if (transaction == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "No transaction found"
                };
            }

            return new Result
            {
                Success = true,
                Transaction = transaction
            };
        }


        public async Task<Result> GetAllTransactionsByCustomerId(long customerId, string transactionType)
        {
            if (customerId <= 333300000001)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid customer ID.\r\nCustomer id starts from 333300000001"
                };
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c=>c.CustomerId == customerId && !c.IsDeleted);
            if (customer == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "No customer found"
                };
            }


            List<Transaction> dalTransactions = new List<Transaction>();
            switch (transactionType.ToLower())
            {
                case "deposit":
                    dalTransactions = await _context.Transactions
                .Where(t => t.Account.CustomerId == customerId  && t.TransactionType.ToLower() == "deposit" && !t.IsDeleted)
                .ToListAsync();
                    break;

                case "withdraw":
                    dalTransactions = await _context.Transactions
                .Where(t => t.Account.CustomerId == customerId && t.TransactionType.ToLower() == "withdraw" && !t.IsDeleted)
                .ToListAsync();
                    break;

                case "all":
                    dalTransactions = await _context.Transactions
                .Where(t => t.Account.CustomerId == customerId  && !t.IsDeleted)
                .ToListAsync();
                    break;

                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = "Invalid account type.\r\nValid values are 'Deposit', 'Withdraw', 'Transfer' and 'All'."
                    };
            }

            if (dalTransactions.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "No transactions found"
                };
            }


            var transactions = _mapper.Map<List<TransactionModel>>(dalTransactions);

            if (transactions.Any())
            {
                return new Result
                {
                    Success = true,
                    Transactions = transactions
                };
            }

            return new Result
            {
                Success = false,
                ErrorMessage = "No transactions found for the customer."
            };
        }

        public async Task<Result> GetAllDeletedTransactionsOfTransactionTypeByCustomerId(string transactionType, long customerId)
        {

            if(customerId <= 333300000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid customer id.\r\nCustomer id starts from 333300000000"
                };
            }

            List<Transaction> dalTransactions = new List<Transaction>();
            switch (transactionType.ToLower())
            {
                case "deposit":
                    dalTransactions = await _context.Transactions
                        .Where(t=>t.TransactionType.ToLower() == "deposit" && t.Account.CustomerId == customerId && t.IsDeleted)
                        .ToListAsync();
                    break;

                case "withdraw":
                     dalTransactions = await _context.Transactions
                        .Where(t => t.TransactionType.ToLower() == "withdraw" && t.Account.CustomerId == customerId && t.IsDeleted)
                        .ToListAsync();
                    break;

                case "all":
                     dalTransactions = await _context.Transactions
                        .Where(t =>t.IsDeleted && t.Account.CustomerId == customerId)
                        .ToListAsync();
                    break;

                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = "Invalid account type.\r\nValid values are 'Deposit', 'Withdraw', 'Transfer' and 'All'."
                    };
            }

            if(dalTransactions.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "No transactions found"
                };
            }

            var transactions = _mapper.Map<List<TransactionModel>>(dalTransactions);

            return new Result
            {
                Success = true,
                Transactions = transactions
            };
        }



        public async Task<Result> GetAllTransactionsBetweenDateRangeOfType(string transactionType, DateTime startDate, DateTime endDate)
        {
            List<Transaction> dalTransactions = new List<Transaction>();

            switch (transactionType.ToLower())
            {
                case "deposit":
                    dalTransactions = await _context.Transactions
                        .Where(t => t.TransactionType.ToLower() == "deposit"
                                    && t.TCreationDate >= startDate && t.TCreationDate <= endDate
                                    && !t.IsDeleted)
                        .ToListAsync();
                    break;

                case "withdraw":
                    dalTransactions = await _context.Transactions
                        .Where(t => t.TransactionType.ToLower() == "withdraw"
                                    && t.TCreationDate >= startDate && t.TCreationDate <= endDate
                                    && !t.IsDeleted)
                        .ToListAsync();
                    break;

                case "all":
                    dalTransactions = await _context.Transactions
                        .Where(t => t.TCreationDate >= startDate && t.TCreationDate <= endDate
                        && !t.IsDeleted)
                        .ToListAsync();
                    break;

                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = "Invalid transaction type. Valid values are 'Deposit', 'Withdraw', 'Transfer', and 'All'."
                    };
            }

            if (dalTransactions.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No {transactionType} transactions found between {startDate.ToShortDateString()} and {endDate.ToShortDateString()}."
                };
            }

            var transactions = _mapper.Map<List<TransactionModel>>(dalTransactions);

            return new Result
            {
                Success = true,
                Transactions = transactions
            };
        }


        public async Task<Result> GetTransactionsBetweenAmountRangeByTransactionType(string transactionType, decimal minAmount, decimal maxAmount)
        {
            List<Transaction> dalTransactions = new List<Transaction>();

            switch (transactionType.ToLower())
            {
                case "deposit":
                    dalTransactions = await _context.Transactions
                        .Where(t => t.TransactionType.ToLower() == "deposit"
                                    && t.Amount >= minAmount && t.Amount <= maxAmount
                                    && !t.IsDeleted)
                        .ToListAsync();
                    break;

                case "withdraw":
                    dalTransactions = await _context.Transactions
                        .Where(t => t.TransactionType.ToLower() == "withdraw"
                                    && t.Amount >= minAmount && t.Amount <= maxAmount
                                    && !t.IsDeleted)
                        .ToListAsync();
                    break;

                case "all":
                    dalTransactions = await _context.Transactions
                        .Where(t => t.Amount >= minAmount && t.Amount <= maxAmount
                                    && !t.IsDeleted)
                        .ToListAsync();
                    break;

                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = "Invalid transaction type. Valid values are 'Deposit', 'Withdraw', 'Transfer', and 'All'."
                    };
            }

            if (dalTransactions.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No {transactionType} transactions found between amount {minAmount} and {maxAmount}."
                };
            }

            var transactions = _mapper.Map<List<TransactionModel>>(dalTransactions);

            return new Result
            {
                Success = true,
                Transactions = transactions
            };
        }


        public async Task<Result> GetTransactionsMadeTo(long receiverAccountId)
        {
            var dalAllReceivers = await _context.Transactions
                .Where(t => t.AccountId == receiverAccountId && t.TransactionType.ToLower() == "deposit" && !t.IsDeleted)
                .ToListAsync();

            if (!dalAllReceivers.Any())
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "No transactions found."
                };
            }

            var transactionIds = dalAllReceivers.Select(t => t.TransactionId).ToList();

            var dalSenders = await _context.Transactions
                .Where(t => transactionIds.Contains(t.TransactionId) && t.TransactionType.ToLower() == "withdraw" && !t.IsDeleted)
                .ToListAsync();

            if (!dalSenders.Any())
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "No sender transactions found."
                };
            }

            var transactions = _mapper.Map<List<TransactionModel>>(dalSenders);

            return new Result
            {
                Success = true,
                Transactions = transactions
            };
        }          

    }
}











//public async Task<Result> RevertAndUpdateTransaction(long transactionId, long newAmount, long newReceiverAccNo)
//{
//    if (transactionId <= 111100000000)
//    {
//        return new Result
//        {
//            Success = false,
//            ErrorMessage = "Invalid Transaction Id\r\nTransaction Id start from 111100000001"
//        };
//    }

//    var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transactionId && !t.IsDeleted);
//    if (transaction == null)
//    {
//        return new Result
//        {
//            Success = false,
//            ErrorMessage = $"Transaction doesn't exist."
//        };
//    }

//    if (transaction.SenderAccountId == transaction.ReceiverAccountId)
//    {
//        if (transaction.TransactionType.ToLower() == "withdraw")
//        {
//            return new Result
//            {
//                Success = false,
//                ErrorMessage = "Transaction type withdraw cannot be reverted"
//            };
//        }
//        else if (transaction.TransactionType.ToLower() == "deposit")
//        {
//            return new Result
//            {
//                Success = false,
//                ErrorMessage = "Transaction type deposit cannot be reverted"
//            };
//        }
//    }

//    //revert
//    var result = await _accountsRepo.Transfer(transaction.ReceiverAccountId, transaction.SenderAccountId, (int)transaction.Amount);
//    if (!result.Success)
//    {
//        return result;
//    }
//    string successMsg = $"Transaction reverted.\r\n{AccountsRepo.ConvertDollarToRupeeSymbol(transaction.Amount)} rupees reverted from Account Id : {transaction.ReceiverAccountId} to Account Id : {transaction.SenderAccountId}\r\n";


//    //sending to new receiverId with the mentioned amount.
//    result = await _accountsRepo.Transfer(transaction.SenderAccountId, newReceiverAccNo, newAmount);
//    if (!result.Success)
//    {
//        return result;
//    }

//    return new Result
//    {
//        Success = true,
//        SuccessMsg = successMsg + $"{AccountsRepo.ConvertDollarToRupeeSymbol(newAmount)} successfully sent to account no {newReceiverAccNo}"
//    };
//}

