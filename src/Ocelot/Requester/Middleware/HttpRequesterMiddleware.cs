using Ocelot.Logging;
using Ocelot.Middleware;
using Ocelot.Responses;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ocelot.Requester.Middleware
{
    public class HttpRequesterMiddleware : OcelotMiddleware
    {
        private readonly OcelotRequestDelegate _next;
        private readonly IHttpRequester _requester;

        public HttpRequesterMiddleware(OcelotRequestDelegate next,
            IOcelotLoggerFactory loggerFactory,
            IHttpRequester requester)
                : base(loggerFactory.CreateLogger<HttpRequesterMiddleware>())
        {
            _next = next;
            _requester = requester;
        }

        public async Task Invoke(DownstreamContext context)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            Response<HttpResponseMessage> response = null;
            try
            {
                response = await _requester.GetResponse(context);
            }
            finally
            {
                watch.Stop();
                Logger.LogInformation($"GetResponse耗时：{watch.ElapsedMilliseconds}");
            }

            if (response.IsError)
            {
                Logger.LogDebug("IHttpRequester returned an error, setting pipeline error");

                SetPipelineError(context, response.Errors);
                return;
            }

            Logger.LogDebug("setting http response message");

            context.DownstreamResponse = new DownstreamResponse(response.Data);

            await _next.Invoke(context);

        }
    }
}
