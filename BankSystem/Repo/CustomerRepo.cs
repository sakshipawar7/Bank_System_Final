using BankSystem.Model; 
using BankSystem.Helpers;
using BankSystem.DAL;
using AutoMapper;
using Microsoft.EntityFrameworkCore; 
using static BankSystem.Helpers.Delegate;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks; 

namespace BankSystem.Repo
{
    public interface ICustomerRepo
    {
        Task<Result> CreateCustomer(CustomerAccountHybridModel c);
        Task<Result> UpdateCustomer(long customerId, JsonPatchDocument<UpdateCustomerModel> patchDoc);
        Task<Result> DeleteCustomer(long customerId);
        Task<Result> GetAllCustomersByBranch(string branch);
        Task<Result> GetCustomerDetailsById(long customerId);
        Task<Result> GetAllAccountsOfCustomerByCustomerId(long customerId);
    }

    public class CustomerRepo : ICustomerRepo
    {
        private readonly IMapper _mapper;
        private readonly BankDbContext _context;
        private readonly AccountDelegate _delegate;
        private readonly IAccountsRepo _accountsRepo;

        public CustomerRepo(IMapper mapper, BankDbContext context, AccountDelegate @delegate, IAccountsRepo accountsRepo)
        {
            _mapper = mapper;
            _context = context;
            _delegate = @delegate;
            _accountsRepo = accountsRepo;
        }



        public async Task<Result> CreateCustomer(CustomerAccountHybridModel c)
        {
            var cus = _mapper.Map<Customer>(c);

            bool checkSavings = string.Equals(c.AccountType, "Savings", StringComparison.OrdinalIgnoreCase);
            bool checkCurrent = string.Equals(c.AccountType, "Current", StringComparison.OrdinalIgnoreCase);

            var existingCustomer = await _context.Customers
                .FirstOrDefaultAsync(cust => (cust.AadharNo == c.AadharNo && cust.PanNo == c.PanNo) || (cust.AadharNo == c.AadharNo || cust.PanNo == c.PanNo) && !cust.IsDeleted);

            if (existingCustomer != null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Customer with the same AadharNo {c.AadharNo} already exists."
                };
            }

            if (!checkSavings && !checkCurrent)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid account type."
                };
            }

            IAccountSavingsOrCurrent obj;
            switch (c.AccountType.ToLower())
            {
                case "savings":
                    obj = _delegate("savings");
                        break;
                case "current":
                    obj = _delegate("current");
                    break;
                default:
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = "Invalid account type\r\nValid account types are 'Savings' and 'Current'."
                    };
            }

            if(c.Amount < obj.MinBalance)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Amount should be atleast {AccountsRepo.ConvertDollarToRupeeSymbol(obj.MinBalance)} for account type {c.AccountType}."
                };
            }

            _context.Customers.Add(cus);
            await _context.SaveChangesAsync();

            var accountDto = new AccountDTO
            {
                AccountType = c.AccountType,
                Amount = c.Amount,
                CustomerId = cus.CustomerId
            };

            var accountService = _delegate(c.AccountType);

            var result = await _accountsRepo.AddAccount(accountDto);
            if (!result.Success)
            {
                return result;
            }

            return new Result
            {
                Success = true,
                SuccessMsg = $"Customer created successfully\r\nYour customer id is {cus.CustomerId}"
            };
        }



        public async Task<Result> UpdateCustomer(long customerId, JsonPatchDocument<UpdateCustomerModel> patchDoc)
        {

            if (customerId <= 333300000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Invalid id {customerId}\r\nCustomer id starts from 333300000001"
                };
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId && !c.IsDeleted);
            if (customer == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Customer not found."
                };
            }

            var updateCustomerModel = _mapper.Map<UpdateCustomerModel>(customer);

            try
            {
                patchDoc.ApplyTo(updateCustomerModel);

                var patchOps = patchDoc.Operations;
                var invalidPaths = patchOps.Where(op => !IsPathValidForUpdateCustomerModel(op.path)).Select(op => op.path).ToList();

                if (invalidPaths.Any())
                {
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = $"Invalid path(s) in JSON Patch document: {string.Join(", ", invalidPaths)}"
                    };
                }

                var validationContext = new ValidationContext(updateCustomerModel);
                var validationResults = new List<ValidationResult>();
                bool isValid = Validator.TryValidateObject(updateCustomerModel, validationContext, validationResults, validateAllProperties: true);

                if (!isValid)
                {
                    var errors = validationResults.Select(vr => vr.ErrorMessage).ToList();
                    return new Result
                    {
                        Success = false,
                        ErrorMessage = "Validation failed.",
                        ValidationErrors = errors
                    };
                }
                _mapper.Map(updateCustomerModel, customer);
                customer.CustomerDetailsUpdateDate = DateTime.Now;
                await _context.SaveChangesAsync();

                return new Result
                {
                    Success = true,
                    Data = updateCustomerModel,
                    SuccessMsg = "Customer details updated successfully"
                };
            }
            catch (JsonPatchException ex)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Invalid JsonPatch operation: {ex.Message}"
                };
            }
        }


        private bool IsPathValidForUpdateCustomerModel(string path)
        {
            var validPaths = new HashSet<string>
            {
                "/Name",
                "/name",
                "/Surname",
                "/surname",
                "/Mobile",
                "/mobile",
                "/Email",
                "/email",
                "/Gender",
                "/gender",
                "/DateOfBirth",
                "/dateofbirth"
            };

            return validPaths.Contains(path);
        }


        public async Task<Result> GetAllCustomersByBranch(string branch)
        {
            var dalCustomers = await _context.Customers
                .Where(c=> c.BranchLocation == branch && !c.IsDeleted)
                .ToListAsync();

            if( dalCustomers.Count == 0)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No customers found of branch {branch}"
                };
            }

            var customers = _mapper.Map<List<DisplayCustomerModel>>(dalCustomers);

            return new Result
            {
                Success = true,
                DisplayAllCustomers = customers
            };
        }


        public async Task<Result> GetCustomerDetailsById(long customerId)
        {
            if(customerId <= 333300000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid customer id.\r\nCustomer id starts from 333300000001"
                };
            }
            var dalCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && !c.IsDeleted);

            if (dalCustomer == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No customers found with customer id {customerId}"
                };
            }

            var customer = _mapper.Map<DisplayCustomerModel>(dalCustomer);

            return new Result
            {
                Success = true,
                DisplayCustomer = customer
            };
        }


        public async Task<Result> GetAllAccountsOfCustomerByCustomerId(long customerId)
        {

            if (customerId <= 333300000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid customer id.\r\nCustomer id starts from 333300000001"
                };
            }

            var customer = await _context.Customers
                .Include(c => c.Accounts)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && !c.IsDeleted);

            if (customer == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"Customer with ID {customerId} not found."
                };
            }

            var accounts = customer.Accounts.ToList();

            var displayAccounts = _mapper.Map<List<DisplayAccountsModel>>(accounts);

            return new Result
            {
                Success = true,
                DisplayAccounts = displayAccounts
            };
        }              


        public async Task<Result> DeleteCustomer(long customerId)
        {
            if (customerId <= 333300000000)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = "Invalid customer id.\r\nCustomer id starts from 333300000001"
                };
            }

            var cus = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId && !c.IsDeleted);
            if (cus == null)
            {
                return new Result
                {
                    Success = false,
                    ErrorMessage = $"No customer found of customer id {customerId}"
                };
            }

           
            var accounts = _context.Accounts.Where(a => a.CustomerId == customerId && !a.IsDeleted).ToList();
            foreach (var account in accounts)
            {
               var result = await _accountsRepo.DeleteAccount(account.AccountNo);
                if (!result.Success)
                {
                    return result;
                }
            }

            cus.IsDeleted = true;
            cus.CustomerDeletionDate = DateTime.Now;
            _context.SaveChanges();
            return new Result
            {
                Success = true,
                SuccessMsg = "Customer deleted successfully."
            };

        }       
    }
}
