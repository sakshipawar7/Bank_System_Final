using BankSystem.Model;
using BankSystem.DAL;
using AutoMapper;
namespace BankSystem.Helpers
{
    public class Mapper : Profile
    {
        public Mapper()
        {
            CreateMap<CustomerAccountHybridModel, Customer>();
            CreateMap<UpdateCustomerModel, Customer>().ReverseMap();
            CreateMap<Customer, DisplayCustomerModel>().ReverseMap();
            CreateMap<Account, DisplayAccountsModel>();
            CreateMap<DisplayAccountsModel, Account>();
            CreateMap<Transaction, TransactionModel>();

        }
    }
}
