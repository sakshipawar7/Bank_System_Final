using BankSystem.Helpers;
using BankSystem.Model;
using BankSystem.Repo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace BankSystem.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerRepo _customerRepo;
        public CustomerController(ICustomerRepo customerRepo)
        {
            _customerRepo = customerRepo;
        }

        [HttpPost("Add Customer")]
        public async Task<IActionResult> AddCustomer([Required] CustomerAccountHybridModel details)
        {
            var result = await _customerRepo.CreateCustomer(details);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }return Ok(result.SuccessMsg);
        }


        [HttpPatch("UpdateCustomer")]
        public async Task<IActionResult> UpdateCustomer([Required] long customerId, [FromBody] JsonPatchDocument<UpdateCustomerModel> patchDoc)
        {
            var result = await _customerRepo.UpdateCustomer(customerId, patchDoc);

            if (!result.Success)
            {
                if (result.ValidationErrors != null && result.ValidationErrors.Any())
                {
                    return BadRequest(new
                    {
                        Message = result.ErrorMessage,
                        Errors = result.ValidationErrors
                    });
                }

                return NotFound(result.ErrorMessage);
            }

            return Ok(new
            {
                result.SuccessMsg,
                result.Data
            });
        }


        [HttpGet("Get Customer Details By Customer Id")]
        public async Task<IActionResult> GetCustomerDetailsById([Required] long customerId)
        {
            var result = await _customerRepo.GetCustomerDetailsById(customerId);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.DisplayCustomer);
        }


        [HttpGet("Get All Customers By Branch")]
        public async Task<IActionResult> GetAllCustomersByBranch([Required] string branch)
        {
            var result = await _customerRepo.GetAllCustomersByBranch(branch);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.DisplayAllCustomers);
        }


        [HttpGet("Get All Accounts Of Customer By Customer Id")]
        public async Task<IActionResult> GetAllAccountsOfCustomerByCustomerId([Required] long customerId)
        {
            var result = await _customerRepo.GetAllAccountsOfCustomerByCustomerId(customerId);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.DisplayAccounts);
        }


        [HttpDelete("Delete Customer")]
        public async Task<IActionResult> DeleteCustomer([Required] long customerId)
        {
            var result = await _customerRepo.DeleteCustomer(customerId);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.SuccessMsg);
        }


        

    }
}
