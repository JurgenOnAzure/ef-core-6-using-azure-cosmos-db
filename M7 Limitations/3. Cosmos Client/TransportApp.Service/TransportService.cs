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

using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TransportApp.Data;
using TransportApp.Domain;

namespace TransportApp.Service
{
  public delegate void WriteLine(string text = "", bool highlight = false, bool isException = false);
  public delegate void WaitForNext(string actionName);

  public class TransportService
  {
    #region Setup

    public TransportService(IDbContextFactory<TransportContext> contextFactory,
      WriteLine writeLine, WaitForNext waitForNext)
    {
      this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
      this.writeLine = writeLine ?? throw new ArgumentNullException(nameof(writeLine));
      this.waitForNext = waitForNext ?? throw new ArgumentNullException(nameof(waitForNext));
    }

    private readonly IDbContextFactory<TransportContext> contextFactory;
    private readonly WriteLine writeLine;
    private readonly WaitForNext waitForNext;

    private async Task RecreateDatabase()
    {
      using var context = await contextFactory.CreateDbContextAsync();

      await context.Database.EnsureDeletedAsync();
      await context.Database.EnsureCreatedAsync();
    }

    #endregion

    public async Task RunSample()
    {
      await RecreateDatabase();

      await AddRandomVehicles();

      using var context = await contextFactory.CreateDbContextAsync();

      var cosmosClient = context.Database.GetCosmosClient();

      var vehicleContainer = cosmosClient.GetContainer(
        "TransportDb", nameof(Vehicle));

      waitForNext(nameof(QueryWithComosClient));
      await QueryWithComosClient(vehicleContainer);

      waitForNext($"{nameof(TryOrderByWithMultipleProperties)} (WITHOUT composite index)");
      await TryOrderByWithMultipleProperties();

      waitForNext(nameof(AddCompositeIndexWithCosmosClient));
      await AddCompositeIndexWithCosmosClient(vehicleContainer);

      waitForNext($"{nameof(TryOrderByWithMultipleProperties)} (WITH composite index)");
      await TryOrderByWithMultipleProperties();
    }

    private async Task AddRandomVehicles()
    {
      writeLine();
      writeLine("Adding random vehicles...");

      const int vehicleCount = 40;
      var random = new Random();
      var utcNow = DateTime.UtcNow;

      using var context = await contextFactory.CreateDbContextAsync();

      // Add random vehicles

      var makeAndModelPairs = new (string Make, string Model)[]
      {
        ("Pluralsight", "Buggy"),
        ("Pluralsight", "Hatchback"),
        ("EF", "Wagon"),
        ("Cosmos", "Coupe"),
        ("Cosmos", "Roadster"),
        ("DotNet", "Van"),
      };

      var vehicles = Enumerable
        .Range(1, vehicleCount)
        .Select(vehicleCounter =>
        {
          var (Make, Model) = makeAndModelPairs[random.Next(0, makeAndModelPairs.Length)];

          var vehicle = new Vehicle
          {
            VehicleId = $"{nameof(Vehicle)}-{vehicleCounter}",
            Make = Make,
            Model = Model,
            Year = (short)random.Next(utcNow.Year - 6, utcNow.Year + 1),
            LicensePlate = $"fictional-{vehicleCounter}",
            Mileage = random.Next(100, 50001),
            PassengerSeatCount = (byte)random.Next(1, 7),
          };


          return vehicle;
        })
        .ToArray();

      context.AddRange(vehicles);

      await context.SaveChangesAsync();

      writeLine("Save successful");
    }

    private async Task QueryWithComosClient(Container vehicleContainer)
    {
      writeLine();
      writeLine("Querying with Cosmos client...");

      var queryDefinition = new QueryDefinition(
        "SELECT c.VehicleId, c.Model, c.Year FROM c WHERE c.Mileage >= @Mileage ORDER BY c.Year")
        .WithParameter("@Mileage", 1000);

      writeLine();
      writeLine(queryDefinition.QueryText, highlight: true);

      using var feedIterator = vehicleContainer.GetItemQueryIterator<VehicleWithYear>(
        queryDefinition: queryDefinition,
        requestOptions: new QueryRequestOptions
        {
          MaxItemCount = 5,
          PartitionKey = new PartitionKey("Pluralsight")
        });

      var pageCounter = 0;
      var itemCounter = 0;

      // for each page...
      while (feedIterator.HasMoreResults)
      {
        writeLine();
        writeLine($"Page {++pageCounter}");
        writeLine();

        // for each item in page...
        foreach (var slimVehicle in await feedIterator.ReadNextAsync())
        {
          writeLine($"  Result {++itemCounter}: {slimVehicle.VehicleId}: {slimVehicle.Model} from {slimVehicle.Year}");
        }
      }
    }

    private async Task TryOrderByWithMultipleProperties()
    {
      writeLine();
      writeLine("Trying ORDER BY on multiple properties...");

      using var context = await contextFactory.CreateDbContextAsync();

      try
      {
        var query = context.Vehicles
          .Where(vehicle => vehicle.Year >= 2020)
          .OrderBy(vehicle => vehicle.Make)
          .ThenByDescending(vehicle => vehicle.Mileage);

        writeLine();
        writeLine(query.ToQueryString(), highlight: true);

        var vehicles = await query
          .ToListAsync();

        writeLine();
        writeLine($"Found {vehicles.Count} vehicles");
      }
      catch (Exception ex)
      {
        writeLine();
        writeLine(ex.Message, isException: true);
      }
    }

    private async Task AddCompositeIndexWithCosmosClient(Container vehicleContainer)
    {
      writeLine();
      writeLine("Adding composite index with Cosmos client...");

      var containerResponse = await vehicleContainer.ReadContainerAsync();

      // add a composite index
      containerResponse.Resource.IndexingPolicy.CompositeIndexes.Add(
        new Collection<CompositePath>
        {
          new CompositePath()
          {
            Path = $"/{nameof(Vehicle.Make)}",
            Order = CompositePathSortOrder.Ascending 
          }
          ,
          new CompositePath()
          {
            Path = $"/{nameof(Vehicle.Mileage)}",
            Order = CompositePathSortOrder.Descending
          }
        });

      // update container with changes
      containerResponse = await vehicleContainer.ReplaceContainerAsync(containerResponse.Resource);

      writeLine();
      writeLine($"Composite index for 'Make, Mileage DESC' status: {containerResponse.StatusCode}");
    }
  }
}