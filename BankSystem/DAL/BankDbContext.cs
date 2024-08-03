using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;

namespace BankSystem.DAL
{
    public class BankDbContext : DbContext
    {
        public BankDbContext(DbContextOptions<BankDbContext> options) : base(options)
        {
        }


        public DbSet<Customer> Customers { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>()
                .HasKey(c => c.CustomerId);

            modelBuilder.Entity<Customer>()
                .HasMany(c => c.Accounts)
                .WithOne(a => a.Customer)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Account>()
                .HasKey(a => a.AccountNo);

            modelBuilder.Entity<Account>()
                .HasOne(a => a.Customer)
                .WithMany(c => c.Accounts)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Transaction>()
            .HasKey(t => new { t.TransactionId, t.AccountId });

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Account) // Transaction has one Sender Account
                .WithMany() 
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Customer>(b =>
            {
                b.ToTable("Customers"); 
                b.Property(a => a.CustomerId)
                    .ValueGeneratedOnAdd()
                    .UseIdentityColumn(333300000001, 1); 
            });

            modelBuilder.Entity<Account>(b =>
            {
                b.ToTable("Accounts"); 
                b.Property(a => a.AccountNo)
                    .ValueGeneratedOnAdd()
                    .UseIdentityColumn(222200000001, 1);
            });

            //modelBuilder.Entity<Transaction>(b =>
            //{
            //    b.ToTable("Transactions"); 
            //    b.Property(a => a.TransactionId)
            //        .ValueGeneratedOnAdd()
            //        .UseIdentityColumn(111100000001, 1); 
            //});


            modelBuilder.Entity<Account>()
                .Property(a => a.Balance)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Transaction>()
                .Property(a => a.Amount)
                .HasColumnType("decimal(18,2)");


            modelBuilder.Entity<Customer>()
            .HasIndex(c => new { c.PanNo, c.AadharNo })
            .IsUnique();


        }
    }
}
