using BankSystem.DAL;
using BankSystem.Repo;
using Microsoft.EntityFrameworkCore;
using BankSystem.Helpers;
using static BankSystem.Repo.AccountsRepo;
using static BankSystem.Helpers.Delegate;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddDbContext<BankDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAutoMapper(typeof(Mapper));

builder.Services.AddScoped<ICustomerRepo, CustomerRepo>();
builder.Services.AddScoped<ITransactionRepo, TransactionRepo>();
builder.Services.AddScoped<Savings>();
builder.Services.AddScoped<Current>();
builder.Services.AddScoped<IAccountsRepo, AccountsRepo>();
builder.Services.AddScoped<AccountDelegate>(provider => type =>
{
    switch (type.ToLower())
    {
        case "savings":
            return provider.GetService<Savings>();
        case "current":
            return provider.GetService<Current>();
        default:
            throw new NotImplementedException($"No service registered for account type {type}");
    }
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
