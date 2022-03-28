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

      waitForNext(nameof(TrackingVsNoTrackingQuery));
      await TrackingVsNoTrackingQuery();

      waitForNext(nameof(BCLQuery));
      await BCLQuery();

      waitForNext(nameof(SelectedPropertiesQuery));
      await SelectedPropertiesQuery();

      waitForNext(nameof(AggregateQueries));
      await AggregateQueries();

      waitForNext(nameof(PartitionKeyQuery));
      await PartitionKeyQuery();

      waitForNext(nameof(SubQueriesWillFail));
      await SubQueriesWillFail();
    }

    private async Task AddRandomItems()
    {
      writeLine();
      writeLine("Adding random items...");

      const int vehicleCount = 40;
      const int tripCount = 50;
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

      // Add random trips

      var stateAndCityPairs = new (string State, string City)[]
      {
        ("Utah", "Draper"),
        ("Utah", "Salt Lake City"),
        ("Texas", "Dallas"),
        ("Kansas", "Topeka")
      };

      var addressCount = driverCount;

      var trips = Enumerable
        .Range(1, tripCount)
        .Select(tripCounter =>
        {

          var (FromState, FromCity) = stateAndCityPairs[random.Next(0, stateAndCityPairs.Length)];
          var (ToState, ToCity) = stateAndCityPairs[random.Next(0, stateAndCityPairs.Length)];

          var trip = new Trip
          {
            TripId = $"{nameof(Trip)}-{tripCounter}",
            BeginUtc = utcNow.AddDays(-1 * random.Next(1, 1001)).AddHours(-1 * random.Next(1, 20)),
            PassengerCount = (byte)random.Next(1, 7),
            DriverId = $"{nameof(Driver)}-{random.Next(1, driverCount + 1)}",
            VehicleId = $"{nameof(Vehicle)}-{random.Next(1, vehicleCount + 1)}",
            FromAddress = new Address
            {
              AddressId = $"{nameof(Address)}-{++addressCount}",
              City = FromCity,
              State = FromState,
              Street = "Fictional street",
              HouseNumber = random.Next(1, 1001).ToString()
            },
            ToAddress = new Address
            {
              AddressId = $"{nameof(Address)}-{++addressCount}",
              City = ToCity,
              State = ToState,
              Street = "Fictional lane",
              HouseNumber = random.Next(1, 1001).ToString()
            }
          };

          if (random.Next(1, 5) == 2)
          {
            trip.EndUtc = trip.BeginUtc.AddMinutes(random.Next(15, 501));
          }

          return trip;
        })
        .ToArray();

      context.AddRange(trips);

      await context.SaveChangesAsync();

      writeLine("Save successful");
    }

    private async Task TrackingVsNoTrackingQuery()
    {
      writeLine();
      writeLine("Getting vehicles...");

      using var queryContext = await contextFactory.CreateDbContextAsync();

      var vehicles = await queryContext.Vehicles
        .Where(vehicle => vehicle.Make == "Pluralsight")
        .ToListAsync();

      var firstVehicle = vehicles[0];

      writeLine($"  State of first entry: {queryContext.Entry(firstVehicle).State}");

      writeLine();
      writeLine("Getting vehicles with .AsNoTracking()...");

      var vehiclesUntracked = await queryContext.Vehicles
        .Where(vehicle => vehicle.Make == "EF")
        .AsNoTracking()
        .ToListAsync();

      var firstVehicleUntracked = vehiclesUntracked[0];

      writeLine($"  State of first entry untracked: {queryContext.Entry(firstVehicleUntracked).State}");

      writeLine();
      writeLine("Changing values...");

      firstVehicle.PassengerSeatCount += 1;
      firstVehicleUntracked.PassengerSeatCount += 1;

      writeLine($"  State of first entry: {queryContext.Entry(firstVehicle).State}");
      writeLine($"  State of first entry untracked: {queryContext.Entry(firstVehicleUntracked).State}");

      var trackedEntities = queryContext.ChangeTracker
        .Entries()
        .Select(entry => entry.Entity)
        .ToList();

      writeLine($"  ChangeTracker contains first entry: {trackedEntities.Contains(firstVehicle)}");
      writeLine($"  ChangeTracker contains first entry untracked: {trackedEntities.Contains(firstVehicleUntracked)}");
    }

    private async Task BCLQuery()
    {
      writeLine();
      writeLine("Getting vehicles with BCL query...");

      using var queryContext = await contextFactory.CreateDbContextAsync();

      var vehicles = await queryContext.Vehicles
        .Where(vehicle => vehicle.Make.Substring(2).ToLower() == "smos"
          && vehicle.Model.ToUpper().Equals("ROADSTER")
          && Math.Round(vehicle.Mileage) > EF.Functions.Random() * 100)
        .ToListAsync();

      writeLine($"  Found {vehicles.Count} vehicle(s)");
    }

    private async Task SelectedPropertiesQuery()
    {
      writeLine();
      writeLine("Projecting some driver properties...");

      using var queryContext = await contextFactory.CreateDbContextAsync();

      var projectedDrivers = await queryContext.Drivers
        .Select(driver => new { FullName = driver.FirstName + " " + driver.LastName.ToUpper() })
        .ToListAsync();

      writeLine($"  Found {projectedDrivers.Count} driver(s), first one = {projectedDrivers[0].FullName}");
    }

    private async Task AggregateQueries()
    {
      writeLine();
      writeLine("Executing aggregates...");

      using var queryContext = await contextFactory.CreateDbContextAsync();

      var maximumVehicleYear = await queryContext.Vehicles
        .MaxAsync(vehicle => vehicle.Year);

      writeLine($"  Newest vehicles are from {maximumVehicleYear}");

      var averageVehicleMileage = await queryContext.Vehicles
        .Where(vehicle => vehicle.Year == maximumVehicleYear)
        .AverageAsync(vehicle => vehicle.Mileage);

      writeLine($"  Average mileage for vehicles from {maximumVehicleYear}: {averageVehicleMileage}");
    }

    private async Task PartitionKeyQuery()
    {
      writeLine();
      writeLine("Finding vehicles with WithPartitionKey()...");

      using var queryContext = await contextFactory.CreateDbContextAsync();

      var vehicles = await queryContext.Vehicles
        .WithPartitionKey("Pluralsight")
        .Where(vehicle => vehicle.Mileage >= 10)
        .OrderByDescending(vehicle => vehicle.Year)
        .ToListAsync();

      writeLine($"  Found {vehicles.Count} vehicles(s)");

      writeLine();
      writeLine("Finding vehicles with partition key in Where...");

      vehicles = await queryContext.Vehicles
        .Where(vehicle => vehicle.Make == "Pluralsight" && vehicle.Mileage >= 10)
        .OrderByDescending(vehicle => vehicle.Year)
        .ToListAsync();

      writeLine($"  Found {vehicles.Count} vehicles(s)");
    }

    private async Task SubQueriesWillFail()
    {
      writeLine();
      writeLine("Trying subqueries...");

      using var queryContext = await contextFactory.CreateDbContextAsync();

      try
      {
        var drivers = await queryContext.Drivers
          .Where(driver => driver.Address != null && driver.Address.State == "Utah")
          .ToListAsync();
      }
      catch (Exception ex)
      {
        writeLine();
        writeLine(ex.Message, isException: true);
      }

      try
      {
        var drivers = await queryContext.Drivers
          .Include(driver => driver.Address)
          .Where(driver => driver.Address != null && driver.Address.State == "Utah")
          .ToListAsync();
      }
      catch (Exception ex)
      {
        writeLine();
        writeLine(ex.Message, isException: true);
      }

      try
      {
        var vehicles = await queryContext.Vehicles
          .Where(vehicle => vehicle.TechnicalSpecifications.Count > 0)
          .ToListAsync();
      }
      catch (Exception ex)
      {
        writeLine();
        writeLine(ex.Message, isException: true);
      }

      try
      {
        var vehicles = await queryContext.Vehicles
          .Where(vehicle => vehicle.TechnicalSpecifications["Maximum Horsepower"] == "123")
          .ToListAsync();
      }
      catch (Exception ex)
      {
        writeLine();
        writeLine(ex.Message, isException: true);
      }

      try
      {
        var vehicles = await queryContext.Vehicles
          .Where(vehicle => vehicle.CheckUpUtcs.Any(checkUpUtc => checkUpUtc >= new DateTime(2019, 1, 1)))
          .ToListAsync();
      }
      catch (Exception ex)
      {
        writeLine();
        writeLine(ex.Message, isException: true);
      }

      // This will work but will get ALL vehicles from Cosmos!

      var vehiclesTheExpensiveWay = (await queryContext.Vehicles
        .ToListAsync())
        .Where(vehicle => vehicle.CheckUpUtcs.Any(checkUpUtc => checkUpUtc >= new DateTime(2019, 1, 1)))
        .ToList();

      writeLine();
      writeLine($"Found {vehiclesTheExpensiveWay.Count} vehicles the 'expensive' way");
    }
  }
}