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

      await AddAddress();

      waitForNext(nameof(SetUnmappedProperties));
      await SetUnmappedProperties();
    }

    private async Task AddAddress()
    {
      writeLine();
      writeLine("Adding address...");

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

      await context.SaveChangesAsync();

      writeLine("Save successful");
    }

    private async Task SetUnmappedProperties()
    {
      writeLine();
      writeLine("Setting unmapped properties...");

      using var context = await contextFactory.CreateDbContextAsync();

      var address = await context.Addresses.FirstAsync();
      var addressEntry = context.Entry(address);

      var addressIdProperty = addressEntry.Property<string>("__id");

      writeLine();
      writeLine($"  __id = '{addressIdProperty.CurrentValue}'");
      writeLine($"  AddressId = '{address.AddressId}'");

      var addressJObject = addressEntry.Property<Newtonsoft.Json.Linq.JObject>("__jObject");

      addressJObject.CurrentValue["UnmappedString"] = "This value is not tracked";
      addressJObject.CurrentValue["UnmappedNumber"] = 1234.567;

      writeLine();
      writeLine($"Entity state after changing unmapped properties = {addressEntry.State}");

      writeLine();
      writeLine($"Setting entity state manually...");

      // State change will not have been triggered
      addressEntry.State = EntityState.Modified;

      await context.SaveChangesAsync();

      writeLine();
      writeLine("Save successful");
    }
  }
}