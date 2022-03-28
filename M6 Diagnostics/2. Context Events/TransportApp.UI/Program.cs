#region Info and license

/*
  This demo application accompanies Pluralsight course 'Using EF Core 6 with Azure Cosmos DB', 
  by Jurgen Kevelaers. See https://pluralsight.pxf.io/efcore6-cosmos.

  MIT License

  Copyright (c) 2022 Jurgen Kevelaers

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
*/

#endregion

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.Title = "Handling Context Events";
Console.WriteLine("Launching...");

var config = new ConfigurationBuilder()
  .AddJsonFile("appsettings.json")
  .Build();

var cosmosConnectionString = config["CosmosConnectionString"];

var services = new ServiceCollection();

services.AddDbContextFactory<TransportApp.Data.TransportContext>(optionsBuilder =>
  optionsBuilder
    .EnableSensitiveDataLogging()
    .UseLoggerFactory(LoggerFactory.Create(builder =>
      builder
        .AddConsole()
        .AddFilter(string.Empty, LogLevel.Information)))
    .ConfigureWarnings(builder =>
      builder
        .Log(
          (CoreEventId.QueryCompilationStarting, LogLevel.Warning))
        .Ignore(new[]
        {
          CosmosEventId.ExecutingSqlQuery
        }))
    .UseCosmos(
      connectionString: cosmosConnectionString,
      databaseName: "TransportDb",
      cosmosOptionsAction: options =>
      {
        options.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Direct);
        options.MaxRequestsPerTcpConnection(20);
        options.MaxTcpConnectionsPerEndpoint(32);
      }));

services.AddTransient<TransportApp.Service.TransportService>();

services.AddSingleton<TransportApp.Service.WriteLine>((text, highlight, isException) =>
{
  if (isException)
  {
    Console.ForegroundColor = ConsoleColor.Red;
  }
  else if (highlight)
  {
    Console.ForegroundColor = ConsoleColor.Yellow;
  }

  Console.WriteLine(text);
  Console.ResetColor();
});

services.AddSingleton<TransportApp.Service.WaitForNext>(actionName =>
{
  Console.ForegroundColor = ConsoleColor.Green;
  Console.WriteLine();
  Console.WriteLine($"Press ENTER to run {actionName}");
  Console.ReadLine();
  Console.Clear();
  Console.WriteLine($"{actionName}:");
  Console.ResetColor();
});

using var serviceProvider = services.BuildServiceProvider();

var transportService = serviceProvider.GetRequiredService<TransportApp.Service.TransportService>();

await transportService.RunSample();

Console.WriteLine();
Console.WriteLine("Done. Press ENTER to quit.");
Console.ReadLine();