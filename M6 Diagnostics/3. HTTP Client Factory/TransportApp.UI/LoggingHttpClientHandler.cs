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

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TransportApp.UI
{
  internal class LoggingHttpClientHandler : HttpClientHandler
  {
    public LoggingHttpClientHandler(ILoggerFactory loggerFactory)
    {
      this.logger = loggerFactory.CreateLogger<LoggingHttpClientHandler>();
    }

    private readonly ILogger logger;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      request.Headers.Add("ps-demo-header", $"demo value {DateTime.UtcNow.Ticks}");

      logger.LogInformation("HTTP REQUEST: {request}", request);

      await LogContentBodyIfPresent(request.Content);

      var response = await base.SendAsync(request, cancellationToken);

      logger.LogInformation("HTTP RESPONSE: {response}", response);

      await LogContentBodyIfPresent(response.Content);

      if (response.StatusCode != System.Net.HttpStatusCode.OK 
        && response.StatusCode != System.Net.HttpStatusCode.NotModified)
      {
        // TODO: something might have gone wrong
        // ...
      }

      return response;
    }

    private async Task LogContentBodyIfPresent(HttpContent? content)
    {
      if (content == null)
      {
        return;
      }

      // NOTE: only do this in debug or tracing scenarios

      var jsonString = await content.ReadAsStringAsync();
      var jsonObject = JsonConvert.DeserializeObject(jsonString);
      var jsonPretty = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);

      logger.LogInformation(jsonPretty);
    }
  }
}
