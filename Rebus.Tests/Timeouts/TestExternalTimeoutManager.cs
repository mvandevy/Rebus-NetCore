﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Rebus.Tests.Contracts.Utilities;
using Rebus.Timeouts;
using Rebus.Transport.InMem;
using Xunit;

#pragma warning disable 1998

namespace Rebus.Tests.Timeouts
{
    public class TestExternalTimeoutManager : FixtureBase
    {
        readonly string _queueName = TestConfig.GetName("client");
        readonly string _queueNameTimeoutManager = TestConfig.GetName("manager");

        readonly ManualResetEvent _gotTheMessage;
        readonly IBus _bus;
        readonly InMemNetwork _network;

        public TestExternalTimeoutManager()
        {
            var logger = new ListLoggerFactory(detailed: true);

            _network = new InMemNetwork();

            // start the external timeout manager
            Configure.With(Using(new BuiltinHandlerActivator()))
                .Logging(l => l.Use(logger))
                .Transport(t => t.UseInMemoryTransport(_network, _queueNameTimeoutManager))
                .Start();

            _gotTheMessage = new ManualResetEvent(false);

            // start the client
            var client = Using(new BuiltinHandlerActivator());

            client.Handle<string>(async str => _gotTheMessage.Set());

            Configure.With(client)
                .Logging(l => l.Use(logger))
                .Transport(t => t.UseInMemoryTransport(_network, _queueName))
                .Timeouts(t => t.UseExternalTimeoutManager(_queueNameTimeoutManager))
                .Start();

            _bus = client.Bus;
        }

        [Fact]
        public async Task ItWorksEvenThoughDeferredMessageIsAccidentallyReceived()
        {
            var headers = new Dictionary<string, string>
            {
                {Headers.DeferredUntil, DateTimeOffset.Now.Add(TimeSpan.FromSeconds(5)).ToIso8601DateTimeOffset()},
                {Headers.DeferredRecipient, _queueName}
            };

            var stopwatch = Stopwatch.StartNew();

            await _bus.SendLocal("denne besked skal stadig udsættes!", headers);

            _gotTheMessage.WaitOrDie(TimeSpan.FromSeconds(8.5), "Message was not received within 8,5 seconds (which it should have been since it was only deferred 5 seconds)");

            Assert.True(stopwatch.Elapsed > TimeSpan.FromSeconds(4.5), "It must take more than 5 second to get the message back (although we allow for a little bit of tolerance in this test....)");
        }

        [Fact]
        public async Task ItWorks()
        {
            var stopwatch = Stopwatch.StartNew();

            await _bus.Defer(TimeSpan.FromSeconds(5), "hej med dig min ven!");

            _gotTheMessage.WaitOrDie(TimeSpan.FromSeconds(8.5), "Message was not received within 8,5 seconds (which it should have been since it was only deferred 5 seconds)");

            Assert.True(stopwatch.Elapsed > TimeSpan.FromSeconds(5), "It must take more than 5 second to get the message back");
        }
    }
}