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

      await AddAddress();

      waitForNext(nameof(ContextEventsWithDebugView));
      await ContextEventsWithDebugView();
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

    private async Task ContextEventsWithDebugView()
    {
      writeLine();
      writeLine("Handling context events...");

      using var eventContext = await contextFactory.CreateDbContextAsync();

      eventContext.ChangeTracker.Tracked += (sender, e) =>
      {
        writeLine();
        writeLine($"{e.Entry.Entity.GetType().Name} is tracked (from query = {e.FromQuery}):", highlight: true);
        writeLine();
      };

      eventContext.ChangeTracker.StateChanged += (sender, e) =>
      {
        writeLine();
        writeLine($"{e.Entry.Entity.GetType().Name} state changed from {e.OldState} to {e.NewState}:", highlight: true);
        writeLine();

        writeLine($"Entity values:");
        writeLine(e.Entry.DebugView.LongView, highlight: true);
        writeLine();

        writeLine();
        writeLine($"Entity metadata:");
        writeLine(e.Entry.Metadata.ToDebugString(), highlight: true);
        writeLine();
      };

      eventContext.SavedChanges += (sender, e) =>
      {
        writeLine();
        writeLine($"Entities saved, {nameof(e.EntitiesSavedCount)} = {e.EntitiesSavedCount}", highlight: true);
      };

      writeLine();
      writeLine($"Retrieving entity...");

      IOrderedQueryable<Address> query = eventContext.Addresses
        .OrderBy(address => address.AddressId);

      writeLine();
      writeLine($"Query string:");
      writeLine();
      writeLine(query.ToQueryString()); // from IQueryable
      writeLine();

      var address = await query
        .FirstAsync();

      writeLine();
      writeLine($"Changing property value...");

      address.Street = "Pluralsight Avenue";

      await eventContext.SaveChangesAsync();
    }
  }
}