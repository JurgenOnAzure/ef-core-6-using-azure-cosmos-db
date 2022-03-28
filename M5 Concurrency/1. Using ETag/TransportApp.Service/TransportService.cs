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

      await AddItems();

      waitForNext(nameof(FindETagProperties));
      await FindETagProperties();

      waitForNext(nameof(CauseAndHandleConflict));
      await CauseAndHandleConflict();
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

      await context.SaveChangesAsync();

      writeLine("Save successful");
    }

    private async Task FindETagProperties()
    {
      writeLine();
      writeLine("Getting ETag properties...");

      using var context = await contextFactory.CreateDbContextAsync();

      var address = await context.Addresses.FindAsync($"{nameof(Address)}-1");
      var driver = await context.Drivers.FindAsync($"{nameof(Driver)}-1");

      var addressEntry = context.Entry(address);
      var driverEntry = context.Entry(driver);

      var addressETagPropertyName = addressEntry.Metadata.GetETagPropertyName();
      var driverETagPropertyName = driverEntry.Metadata.GetETagPropertyName();

      var addressETagValue = addressEntry.Property<string>(addressETagPropertyName).CurrentValue;
      var driverETagValue = driverEntry.Property<string>(driverETagPropertyName).CurrentValue;

      writeLine($"  Address ETag: '{addressETagPropertyName}' = {addressETagValue}");
      writeLine($"  Driver ETag: '{driverETagPropertyName}' = {driverETagValue}");
    }

    private async Task CauseAndHandleConflict()
    {
      writeLine();
      writeLine("Getting same address from 2 contexts...");

      using var context1 = await contextFactory.CreateDbContextAsync();
      using var context2 = await contextFactory.CreateDbContextAsync();

      var addressFromContext1 = await context1.Addresses.FindAsync($"{nameof(Address)}-1");
      var addressFromContext2 = await context2.Addresses.FindAsync($"{nameof(Address)}-1");

      writeLine();
      writeLine($"ETag from context 1 = {addressFromContext1.CustomETag}");
      writeLine($"ETag from context 2 = {addressFromContext2.CustomETag}");

      addressFromContext1.Street = "Street from context 1";
      addressFromContext2.Street = "Street from context 2";

      writeLine();
      writeLine("Saving context 1...");

      await context1.SaveChangesAsync();

      var wasSaved = false;
      var sanityCounter = 0;

      while (!wasSaved && ++sanityCounter < 10)
      {
        try
        {
          writeLine();
          writeLine($"ETag from context 1 = {addressFromContext1.CustomETag}");
          writeLine($"ETag from context 2 = {addressFromContext2.CustomETag}");

          writeLine();
          writeLine("Saving context 2...");

          await context2.SaveChangesAsync();

          writeLine("Save successful");

          writeLine();
          writeLine($"ETag from context 1 = {addressFromContext1.CustomETag}");
          writeLine($"ETag from context 2 = {addressFromContext2.CustomETag}");

          wasSaved = true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
          writeLine();
          writeLine(ex.Message, isException: true);
          writeLine(ex.InnerException?.Message, isException: true);

          // we know we only have 1 entry in this case
          var entry = ex.Entries[0];

          writeLine();
          writeLine($"  {entry.Entity.GetType().Name}:");

          var databaseValues = await entry.GetDatabaseValuesAsync();
          var proposedValues = entry.CurrentValues;

          foreach (var property in proposedValues.Properties
            .Where(property => property.Name != "__jObject"
              && !property.IsConcurrencyToken))
          {
            var propertyName = property.Name;
            var databaseValue = databaseValues[property];
            var proposedValue = proposedValues[property];

            if (!object.Equals(databaseValue, proposedValue))
            {
              writeLine($"    '{propertyName}': original = {databaseValue}, proposed = {proposedValue}");
            }
          }

          var mustSaveConflictingEntity = true; // TODO: apply your custom logic here

          if (!mustSaveConflictingEntity)
          {
            // nothing to do
            break;
          }

          writeLine();
          writeLine("Saving conflicting entity...");

          // skip next concurrency check
          entry.OriginalValues.SetValues(databaseValues);
        }
      }

      if (!wasSaved)
      {
        writeLine();
        writeLine("Conflicting entity was not saved");

        // TODO: 
        // ...
      }
    }
  }
}