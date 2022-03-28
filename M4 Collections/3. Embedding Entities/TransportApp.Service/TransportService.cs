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

      using var defaultContext = await contextFactory.CreateDbContextAsync();

      await AddItemsFromDefaultContext(defaultContext);

      waitForNext(nameof(GetTripFromDefaultContext));
      await GetTripFromDefaultContext(defaultContext);

      waitForNext(nameof(GetTripFromOtherContext));
      await GetTripFromOtherContext();
    }

    private void WriteTripInfo(Trip trip)
    {
      writeLine($"  From address instance available on trip: {(trip.FromAddress == null ? "no" : "yes")}");
      writeLine($"  To address instance available on trip: {(trip.ToAddress == null ? "no" : "yes")}");
      writeLine($"  Driver instance available on trip: {(trip.Driver == null ? "no" : "yes")}");
      writeLine($"  Vehicle instance available on trip: {(trip.Vehicle == null ? "no" : "yes")}");

      if (trip.Driver?.Trips != null)
      {
        writeLine($"  Driver has {trip.Driver.Trips.Count} trip instance(s)");
      }

      if (trip.Vehicle?.Trips != null)
      {
        writeLine($"  Vehicle has {trip.Vehicle.Trips.Count} trip instance(s)");
      }
    }

    private async Task AddItemsFromDefaultContext(TransportContext defaultContext)
    {
      writeLine();
      writeLine("Adding vehicle and driver from DEFAULT context...");

      defaultContext.Add(
        new Vehicle
        {
          VehicleId = $"{nameof(Vehicle)}-1",
          Make = "Pluralsight",
          Model = "Buggy",
          Year = 2018,
          LicensePlate = "2GAT123",
          Mileage = 12800,
          PassengerSeatCount = 6,
          TechnicalSpecifications = new Dictionary<string, string>
          {
            {
              "Maximum Horsepower", "275"
            },
            {
              "Maximum Torque", "262"
            },
            {
              "Fuel Capacity", "25.1"
            },
            {
              "Length (inches)", "219.9"
            },
            {
              "Width (inches)", "81.3"
            }
          },
          CheckUpUtcs = new List<DateTime>
          {
            new DateTime(2019, 2, 12, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 2, 19, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2021, 2, 14, 16, 0, 0, DateTimeKind.Utc)
          }
        });

      defaultContext.Add(
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
        });

      var trip = new Trip
      {
        TripId = $"{nameof(Trip)}-1",
        BeginUtc = new DateTime(2022, 2, 23, 10, 45, 0, DateTimeKind.Utc),
        EndUtc = new DateTime(2022, 2, 23, 11, 17, 0, DateTimeKind.Utc),
        PassengerCount = 2,
        DriverId = $"{nameof(Driver)}-1",
        VehicleId = $"{nameof(Vehicle)}-1",
        FromAddress = new Address
        {
          AddressId = $"{nameof(Address)}-2",
          City = "Salt Lake City",
          State = "Utah",
          Street = "Course Road",
          HouseNumber = "1234"
        },
        ToAddress = new Address
        {
          AddressId = $"{nameof(Address)}-3",
          City = "Rock Springs",
          State = "Wyoming",
          Street = "Lecture Lane",
          HouseNumber = "42"
        }
      };

      defaultContext.Add(trip);

      WriteTripInfo(trip);

      await defaultContext.SaveChangesAsync();

      writeLine("Save successful");
    }

    private async Task GetTripFromDefaultContext(TransportContext defaultContext)
    {
      writeLine();
      writeLine("Getting trip from DEFAULT context...");

      var trip = await defaultContext.Trips.FindAsync($"{nameof(Trip)}-1");

      WriteTripInfo(trip);
    }

    private async Task GetTripFromOtherContext()
    {
      writeLine();
      writeLine("Getting trip from OTHER context...");

      using var otherContext = await contextFactory.CreateDbContextAsync();

      var trip = await otherContext.Trips.FindAsync($"{nameof(Trip)}-1");

      WriteTripInfo(trip);

      // Include is not supported:

      // var trip = otherContext.Trips
      //   .Include(trip => trip.Driver)
      //   .FirstAsync(trip => trip.TripId == $"{nameof(Trip)}-1");
    }
  }
}