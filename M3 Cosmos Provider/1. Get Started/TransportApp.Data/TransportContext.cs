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
using TransportApp.Domain;

namespace TransportApp.Data
{
  public class TransportContext : DbContext
  {
    public TransportContext(DbContextOptions<TransportContext> options)
      : base(options)
    { }

    #region DbSets

    public DbSet<Driver> Drivers { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Trip> Trips { get; set; }

    #endregion

    // If you're not using dependency injection to configure the context, 
    // configure it here:

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
    //  optionsBuilder
    //    .UseCosmos(
    //      connectionString: cosmosConnectionString,
    //      databaseName: "TransportDb",
    //      cosmosOptionsAction: options =>
    //      {
    //        options.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Direct);
    //        options.MaxRequestsPerTcpConnection(20);
    //        options.MaxTcpConnectionsPerEndpoint(32);
    //      });

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      // TODO
    }
  }
}
