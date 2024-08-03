using BankSystem.Helpers;
using BankSystem.Repo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace BankSystem.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionRepo _transactionRepo;

        public TransactionController(ITransactionRepo transactionRepo)
        {
            _transactionRepo = transactionRepo;
        }

        [HttpPost("Revert Transaction")]
        public async Task<IActionResult> RevertTransaction([Required] long tid)
        {
            var result = await _transactionRepo.RevertTransaction(tid);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.SuccessMsg);
        }
                

        [HttpGet("Get Transaction Summary By Account No")]
        public async Task<IActionResult> GetTransactionSummaryOfAccountNo([Required] long accountNo,[Required] string transactionType)
        {
            var result = await _transactionRepo.GetTransactionSummaryOfAccountNo(accountNo, transactionType);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Transactions);
        }


        [HttpGet("Get Transaction By Transaction Id")]
        public async Task<IActionResult> GetTransactionByTransactionId([Required] long transactionId)
        {
            var result = await _transactionRepo.GetTransactionByTransactionId(transactionId);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Transaction);
        }


        [HttpGet("Get All Transactions By Customer Id")]
        public async Task<IActionResult> GetAllTransactionsByCustomerId([Required] long customerId, [Required] string transactionType)
        {
            var result = await _transactionRepo.GetAllTransactionsByCustomerId(customerId, transactionType);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Transactions);
        }


        [HttpGet("Get All Deleted Transactions Of Transaction Type By Customer Id")]
        public async Task<IActionResult> GetAllDeletedTransactionsOfTransactionTypeByCustomerId([Required] string transactionType, [Required] long customerId)
        {
            var result = await _transactionRepo.GetAllDeletedTransactionsOfTransactionTypeByCustomerId(transactionType, customerId);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Transactions);
        }


        [HttpGet("Get All Transactions Between Date Range Of Type")]
        public async Task<IActionResult> GetAllTransactionsBetweenDateRangeOfType([Required] string transactionType, [Required] DateTime startDate, [Required] DateTime endDate)
        {
            var result = await _transactionRepo.GetAllTransactionsBetweenDateRangeOfType(transactionType, startDate, endDate);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Transactions);
        }


        [HttpGet("Get Transactions Between Amount Range By Transaction Type")]
        public async Task<IActionResult> GetTransactionsBetweenAmountRangeByTransactionType([Required] string transactionType, [Required] decimal minAmount, [Required] decimal maxAmount)
        {
            var result = await _transactionRepo.GetTransactionsBetweenAmountRangeByTransactionType(transactionType, minAmount, maxAmount);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Transactions);
        }


        [HttpGet("Get Transactions Made To")]
        public async Task<IActionResult> GetTransactionsMadeTo(long receiverAccountId)
        {
            var result = await _transactionRepo.GetTransactionsMadeTo(receiverAccountId);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Transactions);
        }


        //[HttpPost("Revert And Send To New Account")]
        //public async Task<IActionResult> RevertAndUpdateTransaction([Required] long tid, [Required] long newAmount, [Required] long newReceiverAccNo)
        //{
        //    var result = await _transactionRepo.RevertAndUpdateTransaction(tid, newAmount, newReceiverAccNo);
        //    if (!result.Success)
        //    {
        //        return BadRequest(result.ErrorMessage);
        //    }return Ok(result.SuccessMsg);
        //}


    }
}
