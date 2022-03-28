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
using TransportApp.Data;
using TransportApp.Domain;

namespace TransportApp.Service
{
  public delegate void WriteLine(string text = "", bool highlight = false, bool isException = false);
  public delegate void WaitForNext(string actionName);

  public class TransportService
  {
    #region Setup

    public TransportService(IDbContextFactory<TransportContext> contextFactory, WriteLine writeLine, WaitForNext waitForNext)
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

      await AddRandomItems();

      waitForNext(nameof(SimpleRawSQLQuery));
      await SimpleRawSQLQuery();

      waitForNext(nameof(EmbeddedDictionaryRawSQLQuery));
      await EmbeddedDictionaryRawSQLQuery();

      waitForNext(nameof(EmbeddedListRawSQLQuery));
      await EmbeddedListRawSQLQuery();
    }

    private async Task AddRandomItems()
    {
      writeLine();
      writeLine("Adding random items...");

      const int vehicleCount = 40;
      var random = new Random();
      var utcNow = DateTime.UtcNow;

      using var context = await contextFactory.CreateDbContextAsync();

      // Add drivers

      var drivers = new[]
      {
        new Driver
        {
          DriverId = $"{nameof(Driver)}-1",
          FirstName = "Jurgen",
          LastName = "Kevelaers",
          EmploymentBeginUtc = new DateTime(2022, 1, 17, 9, 0, 0, DateTimeKind.Utc),
          Address = new Address
          {
            AddressId = $"{nameof(Address)}-1",
            City = "Draper",
            State = "Utah",
            Street = "Class Street",
            HouseNumber = "98"
          }
        },
        new Driver
        {
          DriverId = $"{nameof(Driver)}-2",
          FirstName = "Jane",
          LastName = "Doe",
          EmploymentBeginUtc = new DateTime(2020, 5, 4, 9, 0, 0, DateTimeKind.Utc),
          Address = new Address
          {
            AddressId = $"{nameof(Address)}-2",
            City = "Dallas",
            State = "Texas",
            Street = "Course Lane",
            HouseNumber = "243"
          }
        },
        new Driver
        {
          DriverId = $"{nameof(Driver)}-3",
          FirstName = "John",
          LastName = "Doe",
          EmploymentBeginUtc = new DateTime(2021, 8, 9, 9, 0, 0, DateTimeKind.Utc),
          Address = new Address
          {
            AddressId = $"{nameof(Address)}-3",
            City = "Topeka",
            State = "Kansas",
            Street = "Lesson Street",
            HouseNumber = "1A"
          }
        }
      };

      context.AddRange(drivers);

      var driverCount = drivers.Length;

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
            TechnicalSpecifications = new Dictionary<string, string>
            {
              {
                "Maximum Horsepower", random.Next(40, 221).ToString()
              },
              {
                "Maximum Torque", random.Next(20, 201).ToString()
              }
            }
          };

          if (random.Next(1, 3) == 2)
          {
            vehicle.TechnicalSpecifications["IsAutomatic"] = random.Next(1, 3) == 1 ? "Yes" : "No";
          }

          // add one checkup for each year between last year and the vehicle's year

          var checkUpLastYear = utcNow.Year - 1;
          var checkUpCount = checkUpLastYear - vehicle.Year;

          if (checkUpCount > 0)
          {
            var checkUpFirstYear = checkUpLastYear - checkUpCount + 1;

            vehicle.CheckUpUtcs = Enumerable
              .Range(checkUpFirstYear, checkUpCount)
              .Select(checkUpYear =>
              {
                var checkUpMonth = random.Next(4, 7);
                var checkUpDay = random.Next(1, 28);
                var checkUpHour = random.Next(9, 18);

                var checkUpUtc = new DateTime(checkUpYear, checkUpMonth, checkUpDay, checkUpHour, 0, 0, DateTimeKind.Utc);

                return checkUpUtc;
              })
              .ToList();
          }

          return vehicle;
        })
        .ToArray();

      context.AddRange(vehicles);

      await context.SaveChangesAsync();

      writeLine("Save successful");
    }

    private async Task SimpleRawSQLQuery()
    {
      writeLine();
      writeLine("Executing simple raw SQL query...");

      using var context = await contextFactory.CreateDbContextAsync();

      var sql = "SELECT * FROM c "
        + "WHERE c.FirstName = {0} and c.EmploymentBeginUtc <= {1}";

      var parameters = new object[]
      {
        "Jurgen",
        DateTime.UtcNow
      };

      var query = context.Drivers
        .FromSqlRaw(sql, parameters);

      writeLine();
      writeLine(query.ToQueryString(), highlight: true);

      var drivers = await query
        .ToListAsync();

      writeLine();
      writeLine($"Found {drivers.Count} driver(s)");
    }

    private async Task EmbeddedDictionaryRawSQLQuery()
    {
      writeLine();
      writeLine("Executing embedded dictionary raw SQL query...");

      using var context = await contextFactory.CreateDbContextAsync();

      // This won't work:

      // var vehicles = await context.Vehicles
      //  .Where(vehicle => vehicle.TechnicalSpecifications["IsAutomatic"] == "Yes")
      //  .ToListAsync();

      // This SHOULD work:

      var sql = "SELECT * FROM c "
        + "WHERE c.TechnicalSpecifications[{0}] = {1}";

      var parameters = new object[]
      {
        "IsAutomatic",
        "Yes"
      };

      var query = context.Vehicles
        .FromSqlRaw(sql, parameters);

      writeLine();
      writeLine(query.ToQueryString(), highlight: true);

      var vehicles = await query
        .ToListAsync();

      writeLine();
      writeLine($"Found {vehicles.Count} vehicle(s)");
    }

    private async Task EmbeddedListRawSQLQuery()
    {
      writeLine();
      writeLine("Executing embedded list raw SQL query...");

      using var context = await contextFactory.CreateDbContextAsync();

      // This won't work:

      // var vehicles = await context.Vehicles
      //  .Where(vehicle => vehicle.CheckUpUtcs.Any(checkUpUtc => checkUpUtc >= DateTime.UtcNow.AddYears(-3)))
      //  .ToListAsync();

      // This SHOULD work:

      var sql = "SELECT * FROM c "
        + "WHERE EXISTS ("
        + "SELECT VALUE CheckUpUtc "
        + "FROM CheckUpUtc IN c.CheckUpUtcs "
        + "WHERE CheckUpUtc > {0})";

      var parameters = new object[]
      {
        DateTime.UtcNow.AddYears(-3)
      };

      var query = context.Vehicles
        .FromSqlRaw(sql, parameters);

      writeLine();
      writeLine(query.ToQueryString(), highlight: true);

      var vehicles = await query
        .ToListAsync();

      writeLine();
      writeLine($"Found {vehicles.Count} vehicle(s)");
    }
  }
}