using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

var factory = new OrderImportContextFactory();
using var context = factory.CreateDbContext(args);

#region Logic
if (args.Length == 3)
{
    var customers = (await File.ReadAllLinesAsync(args[1])).Skip(1).Select(x => x.Split('\t'));
    var orders = (await File.ReadAllLinesAsync(args[2])).Skip(1).Select(x => x.Split('\t'));
    switch (args[0])
    {
        case "import":
            Import(customers, orders);
            break;
        case "full":
            Clean();
            Import(customers, orders);
            Check();
            break;
        default:
            Console.Error.WriteLine("Unknown Command-line Arguments");
            break;
    }
}
else if (args.Length == 1)
{
    switch (args[0])
    {
        case "clean":
            Clean();
            break;
        case "check":
            Check();
            break;
        default:
            Console.Error.WriteLine("Unknown Command-line Arguments");
            break;
    }
}
else
{
    Console.Error.WriteLine("Unknown Command-line Arguments");
}
#endregion

#region Methods
void Import(IEnumerable<string[]> customers, IEnumerable<string[]> orders)
{
    var newCustomers = customers
        .Select(x => new Customer { Name = x[0], CreditLimit = Convert.ToDecimal(x[1]), Orders = orders
        .Where(y => y[0] == x[0])
        .Select(y => new Order { OrderDate = Convert.ToDateTime(y[1]), OrderValue = Convert.ToDecimal(y[2]) })
        .ToList() });

    context.Customers.AddRange(newCustomers);

    context.SaveChanges();
}

void Clean()
{
    //Delete every row
    context.Customers.RemoveRange(context.Customers);
    context.Orders.RemoveRange(context.Orders);

    //Reset the PK's
    context.Database.ExecuteSqlRaw("DBCC CHECKIDENT('Orders', RESEED, 0)");
    context.Database.ExecuteSqlRaw("DBCC CHECKIDENT('Customers', RESEED, 0)");

    context.SaveChanges();
}

void Check()
{
    var customersExceeded = context.Customers
        .Where(x => x.Orders.Sum(y => y.OrderValue) > x.CreditLimit)
        .ToList();
    foreach (var c in customersExceeded) Console.WriteLine($"Customer {c.Id} has exceeded his credit limit.");
}
#endregion

#region Create the model class
class Customer
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Index(IsUnique = true)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(8, 2)")]
    public decimal CreditLimit { get; set; }

    public List<Order> Orders { get; set; } = new();
}

class Order
{
    public int Id { get; set; }

    public Customer? Customer { get; set; }

    public int CustomerId { get; set; }

    public DateTime OrderDate { get; set; }

    [Column(TypeName = "decimal(8, 2)")]
    public decimal OrderValue { get; set; }

}

class OrderImportContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }

    public DbSet<Order> Orders { get; set; }

    public OrderImportContext(DbContextOptions<OrderImportContext> options)
        : base(options)
    {

    }
}

class OrderImportContextFactory : IDesignTimeDbContextFactory<OrderImportContext>
{
    public OrderImportContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<OrderImportContext>();
        optionsBuilder
            // Uncomment the following line if you want to print generated
            // SQL statements on the console.
            //.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new OrderImportContext(optionsBuilder.Options);
    }
}
#endregion