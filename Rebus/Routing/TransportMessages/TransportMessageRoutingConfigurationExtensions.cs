﻿using System;
using System.Threading.Tasks;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Transport;

namespace Rebus.Routing.TransportMessages
{
    /// <summary>
    /// Configuration extensions for very fast filtering and forwarding of incoming transport messages
    /// </summary>
    public static class TransportMessageRoutingConfigurationExtensions
    {
        /// <summary>
        /// Adds the given routing function - should return <see cref="ForwardAction.None"/> to do nothing, or another action
        /// available on <see cref="ForwardAction"/> in order to do something to the message
        /// </summary>
        public static void AddTransportMessageForwarder(this StandardConfigurer<IRouter> configurer, Func<TransportMessage, Task<ForwardAction>> routingFunction)
        {
            configurer.OtherService<IPipeline>()
                .Decorate(c =>
                {
                    var pipeline = c.Get<IPipeline>();
                    var transport = c.Get<ITransport>();
                    var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();

                    var stepToAdd = new ForwardTransportMessageStep(routingFunction, transport, rebusLoggerFactory);

                    return new PipelineStepConcatenator(pipeline)
                        .OnReceive(stepToAdd, PipelineAbsolutePosition.Front);
                });
        }

    }
}