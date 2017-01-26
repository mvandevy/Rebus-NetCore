﻿using System;
using System.Collections.Generic;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Utilities;
using Rebus.Transport.InMem;
using Xunit;

#pragma warning disable 1998

namespace Rebus.Tests.Routing
{
    public class TestTransportMessageOperations : FixtureBase
    {
        const string ForwardedMessagesQueue = "forwarded messages";
        readonly InMemNetwork _network;
        readonly BuiltinHandlerActivator _forwarderActivator;
        readonly BuiltinHandlerActivator _receiverActivator;

        public TestTransportMessageOperations()
        {
            _network = new InMemNetwork();

            _forwarderActivator = Using(new BuiltinHandlerActivator());
            
            Configure.With(_forwarderActivator)
                .Transport(t => t.UseInMemoryTransport(_network, "message forwarder"))
                .Start();

            _receiverActivator = Using(new BuiltinHandlerActivator());
            
            Configure.With(_receiverActivator)
                .Transport(t => t.UseInMemoryTransport(_network, ForwardedMessagesQueue))
                .Start();
        }

        [Fact]
        public void CanForwardMessageToErrorQueue()
        {
            var sharedCounter = new SharedCounter(1) { Delay = TimeSpan.FromSeconds(0.1) };

            Using(sharedCounter);

            _forwarderActivator.Handle<string>(async (bus, str) =>
            {
                await bus.Advanced.TransportMessage.Forward(ForwardedMessagesQueue, new Dictionary<string, string> {{"testheader", "OK"}});
            });

            _receiverActivator.Handle<string>(async (bus, context, str) =>
            {
                var headers = context.TransportMessage.Headers;

                if (!headers.ContainsKey("testheader"))
                {
                    sharedCounter.Fail("Could not find 'testheader' header!");
                    return;
                }

                var headerValue = headers["testheader"];
                if (headerValue != "OK")
                {
                    sharedCounter.Fail("'testheader' header had value {0}", headerValue);
                    return;
                }

                sharedCounter.Decrement();
            });

            _forwarderActivator.Bus.SendLocal("hej med dig min ven!!!").Wait();

            sharedCounter.WaitForResetEvent();
        }

        [Fact]
        public void CanDeferTransportMessage()
        {
            var counter = new SharedCounter(1);

            Using(counter);

            var customHeaders = new Dictionary<string, string>
            {
                {"testheader", "customizzle valuizzle"}
            };

            var didDeferTheMessage = false;

            _forwarderActivator.Handle<string>(async (bus, str) =>
            {
                if (!didDeferTheMessage)
                {
                    Console.WriteLine("Got the message for the first time - deferring it!");

                    await bus.Advanced.TransportMessage.Defer(TimeSpan.FromSeconds(2), customHeaders);

                    didDeferTheMessage = true;

                    return;
                }

                Console.WriteLine("Got the message after it was deferred... nice!");

                counter.Decrement();
            });

            _forwarderActivator.Bus.SendLocal("hej med dig min ven!!!").Wait();

            counter.WaitForResetEvent();
        }
    }
}