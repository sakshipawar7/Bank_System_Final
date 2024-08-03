using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BankSystem.Repo;
using BankSystem.Model;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using BankSystem.Helpers;

namespace BankSystem.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountsRepo _accountsRepo;

        public AccountController(IAccountsRepo accountsRepo)
        {
            _accountsRepo = accountsRepo;
        }

        [HttpPost("Create Account")]
        public async Task<IActionResult> CreateAccount([FromBody][Required] AccountDTO accountDTO)
        {
            var result = await _accountsRepo.AddAccount(accountDTO);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.SuccessMsg);
        }


        [HttpGet("Get All Accounts")]
        public async Task<IActionResult> GetAllAccounts([FromQuery][Required] string accountType)
        {
            var result = await _accountsRepo.GetAllAccounts(accountType);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.DisplayAccounts);
        }


        [HttpPost("Convert Account To")]
        public async Task<IActionResult> ConvertAccountTo([Required] long accountNo, [Required] string accountType)
        {
            var result = await _accountsRepo.ConvertAccountTo(accountNo, accountType);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.SuccessMsg);
        }


        [HttpGet("Get All Customers Of Account Type")]
        public async Task<IActionResult> GetAllCustomersOfAccountType([Required] string accountType)
        {
            var result = await _accountsRepo.GetAllCustomersOfAccountType(accountType);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.DisplayAllCustomers);
        }


        [HttpPost("Transfer Amount")]
        public async Task<IActionResult> TransferAndAddTransaction([Required] long senderAccountId, [Required] long receiverAccountId, [Required] long amount)
        {
            var result = await _accountsRepo.Transfer(senderAccountId, receiverAccountId, amount);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.SuccessMsg);
        }


        [HttpPost("Deposit")]
        public async Task<IActionResult> DeposiAndAddTransaction([Required] long accountNo, [Required] long amount)
        {
            var result = await _accountsRepo.DepositOrWithdraw(accountNo, amount, "deposit");
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.SuccessMsg);
        }


        [HttpPost("Withdraw")]
        public async Task<IActionResult> WithdrawAndAddTransaction([Required] long accountNo, [Required] long amount)
        {
            var result = await _accountsRepo.DepositOrWithdraw(accountNo, amount, "withdraw");
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.SuccessMsg);
        }


        [HttpGet("Get Account Details By Account No")]
        public async Task<IActionResult> GetAccountDetailsByAccountNo([Required] long accountNo)
        {
            var result = await _accountsRepo.GetAccountDetailsByAccountNo(accountNo);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.DisplayAccount);
        }


        [HttpGet("Get All Accounts Of Type Created Between")]
        public async Task<IActionResult> GetAllAccountsOfTypeCreatedBetween([Required] string accountType, [Required] DateTime startDate, [Required] DateTime endDate)
        {
            var result = await _accountsRepo.GetAllAccountsOfTypeCreatedBetween(accountType, startDate, endDate);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.DisplayAccounts);
        }


        [HttpGet("Get All Customers Of Account Type Created Between")]
        public async Task<IActionResult> GetAllCustomersOfAccountTypeCreatedBetween([Required] string accountType, [Required] DateTime startDate, [Required] DateTime endDate)
        {
            var result = await _accountsRepo.GetAllCustomersOfAccountTypeCreatedBetween(accountType, startDate,endDate);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.DisplayAllCustomers);
        }


        [HttpGet("Get All Accounts Of Account Type Converted Between")]
        public async Task<IActionResult> GetAllAccountsOfTypeConvertedBetween([Required] string accountType,[Required] DateTime startDate, [Required] DateTime endDate)
        {
            var result = await _accountsRepo.GetAllAccountsOfTypeConvertedBetween(accountType,startDate,endDate);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.DisplayAccounts);
        }


        [HttpGet("Get All Accounts Of Account Type Having Balance Between")]
        public async Task<IActionResult> GetAllAccountsOfTypeHavingBalanceBetween([Required] string accountType,[Required] decimal minBalance, [Required] decimal maxBalance)
        {
            var result = await _accountsRepo.GetAllAccountsOfTypeHavingBalanceBetween(accountType, minBalance, maxBalance);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.DisplayAccounts);
        }


        [HttpGet("Get Account Terms By Account Type")]
        public IActionResult GetAccountTerms(string accountType)
        {
            var result = _accountsRepo.GetAccountTermsByAccountType(accountType);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Data);
        }     


        [HttpDelete("Delete Account")]
        public async Task<IActionResult> DeleteAccount([Required] long accountNo)
        {
            var result = await _accountsRepo.DeleteAccount(accountNo);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.SuccessMsg);
        }
    }
}
