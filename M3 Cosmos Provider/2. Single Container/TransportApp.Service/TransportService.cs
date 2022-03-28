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

  public class TransportService
  {
    #region Setup

    public TransportService(IDbContextFactory<TransportContext> contextFactory, WriteLine writeLine)
    {
      this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
      this.writeLine = writeLine ?? throw new ArgumentNullException(nameof(writeLine));
    }

    private readonly IDbContextFactory<TransportContext> contextFactory;
    private readonly WriteLine writeLine;

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

      await AddItems();
    }

    private async Task AddItems()
    {
      writeLine();
      writeLine("Adding items...");

      using var context = await contextFactory.CreateDbContextAsync();

      context.Add(
        new Address
        {
          AddressId = $"{nameof(Address)}-1",
          City = "Salt Lake City",
          State = "Utah",
          Street = "Course Road",
          HouseNumber = "1234"
        });

      context.Add(
        new Driver
        {
          DriverId = $"{nameof(Driver)}-1",
          FirstName = "Jurgen",
          LastName = "Kevelaers",
          EmploymentBeginUtc = new DateTime(2022, 1, 17, 9, 0, 0, DateTimeKind.Utc)
        });

      context.Add(
        new Vehicle
        {
          VehicleId = $"{nameof(Vehicle)}-1",
          Make = "Pluralsight",
          Model = "Buggy",
          Year = 2018,
          LicensePlate = "2GAT123",
          Mileage = 12800,
          PassengerSeatCount = 6
        });

      context.Add(
        new Trip
        {
          TripId = $"{nameof(Trip)}-1",
          BeginUtc = new DateTime(2022, 3, 23, 10, 45, 0, DateTimeKind.Utc),
          EndUtc = new DateTime(2022, 3, 23, 11, 17, 0, DateTimeKind.Utc),
          PassengerCount = 2
        });

      await context.SaveChangesAsync();

      writeLine("Save successful");
    }
  }
}