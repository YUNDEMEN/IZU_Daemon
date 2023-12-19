

using IZIII;
using Microsoft.Extensions.Hosting.WindowsServices;
using Topshelf;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService()
                                     ? AppContext.BaseDirectory : default
};


var builder = WebApplication.CreateBuilder(options);
builder.Host.UseWindowsService();
// Add services to the container.

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
await app.RunAsync();
