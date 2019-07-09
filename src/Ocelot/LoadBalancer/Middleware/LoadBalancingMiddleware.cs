﻿using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Logging;
using Ocelot.Middleware;
using Ocelot.Responses;
using Ocelot.Values;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Ocelot.LoadBalancer.Middleware
{
    public class LoadBalancingMiddleware : OcelotMiddleware
    {
        private readonly OcelotRequestDelegate _next;
        private readonly ILoadBalancerHouse _loadBalancerHouse;

        public LoadBalancingMiddleware(OcelotRequestDelegate next,
            IOcelotLoggerFactory loggerFactory,
            ILoadBalancerHouse loadBalancerHouse)
                : base(loggerFactory.CreateLogger<LoadBalancingMiddleware>())
        {
            _next = next;
            _loadBalancerHouse = loadBalancerHouse;
        }

        public async Task Invoke(DownstreamContext context)
        {
            var loadBalancer = await _loadBalancerHouse.Get(context.DownstreamReRoute, context.Configuration.ServiceProviderConfiguration);
            if (loadBalancer.IsError)
            {
                Logger.LogDebug("there was an error retriving the loadbalancer, setting pipeline error");
                SetPipelineError(context, loadBalancer.Errors);
                return;
            }

            Response<ServiceHostAndPort> hostAndPort = null;
            Stopwatch watch = new Stopwatch();
            try
            {
                watch.Start();
                hostAndPort = await loadBalancer.Data.Lease(context);
            }
            finally
            {
                watch.Stop();
                Logger.LogInformation($"获取服务耗时:{watch.ElapsedMilliseconds}");
            }
            
            if (hostAndPort.IsError)
            {
                Logger.LogDebug("there was an error leasing the loadbalancer, setting pipeline error");
                SetPipelineError(context, hostAndPort.Errors);
                return;
            }

            context.DownstreamRequest.Host = hostAndPort.Data.DownstreamHost;

            if (hostAndPort.Data.DownstreamPort > 0)
            {
                context.DownstreamRequest.Port = hostAndPort.Data.DownstreamPort;
            }

            try
            {
                await _next.Invoke(context);
            }
            catch (Exception)
            {
                Logger.LogDebug("Exception calling next middleware, exception will be thrown to global handler");
                throw;
            }
            finally
            {
                loadBalancer.Data.Release(hostAndPort.Data);
            }
        }
    }
}
