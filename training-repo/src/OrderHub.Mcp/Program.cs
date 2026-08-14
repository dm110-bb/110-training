using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderHub.Core.Interfaces;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Data;
using OrderHub.Infrastructure.Repositories;
using OrderHub.Mcp;

var builder = Host.CreateApplicationBuilder(args);

// stdout is reserved for the MCP protocol. Send every log message to stderr.
builder.Logging.AddConsole(options =>
    options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddDbContext<OrderHubDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")
        ?? "Server=localhost;Database=OrderHubTraining;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"));

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<OrderHubTools>()
    .WithResources<OrderHubResources>()
    .WithPrompts<OrderHubPrompts>();

await builder.Build().RunAsync();
