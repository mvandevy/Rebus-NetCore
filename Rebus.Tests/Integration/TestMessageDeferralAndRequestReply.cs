﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Utilities;
using Rebus.Transport.InMem;
using Xunit;

#pragma warning disable 1998

namespace Rebus.Tests.Integration
{
    public class TestMessageDeferralAndRequestReply : FixtureBase
    {
        readonly BuiltinHandlerActivator _service;
        readonly BuiltinHandlerActivator _client;
        readonly Stopwatch _stopwatch;

        public TestMessageDeferralAndRequestReply()
        {
            var inMemNetwork = new InMemNetwork();

            _stopwatch = Stopwatch.StartNew();

            _service = CreateEndpoint(inMemNetwork, "service");
            _client = CreateEndpoint(inMemNetwork, "client");
        }

        BuiltinHandlerActivator CreateEndpoint(InMemNetwork inMemNetwork, string inputQueueName)
        {
            var service = Using(new BuiltinHandlerActivator());

            Configure.With(service)
                .Logging(l => l.Console(minLevel: LogLevel.Warn))
                .Transport(t => t.UseInMemoryTransport(inMemNetwork, inputQueueName))
                .Routing(r => r.TypeBased().Map<string>("service"))
                .Start();

            return service;
        }

        // Defers the message with bus.Defer, which sends the message as a new message (and therefore needs to transfer the ReturnAddress in order to be able to bus.Reply later)
        [Fact]
        public async Task DeferringRequestDoesNotBreakAbilityToReply_DeferWithMessageApi()
        {
            _service.Handle<string>(async (bus, context, str) =>
            {
                const string deferredMessageHeader = "this message was already deferred";

                if (!context.TransportMessage.Headers.ContainsKey(deferredMessageHeader))
                {
                    var extraHeaders = new Dictionary<string, string>
                    {
                        {deferredMessageHeader, ""},
                        {Headers.ReturnAddress, context.Headers[Headers.ReturnAddress]}
                    };

                    Console.WriteLine($"SERVICE deferring '{str}' 1 second (elapsed: {_stopwatch.Elapsed.TotalSeconds:0.# s})");
                    await bus.Defer(TimeSpan.FromSeconds(1), str, extraHeaders);
                    return;
                }

                const string reply = "yeehaa!";
                Console.WriteLine($"SERVICE replying '{reply}'  (elapsed: {_stopwatch.Elapsed.TotalSeconds:0.# s})");
                await bus.Reply(reply);
            });

            await RunDeferTest();
        }

        // Defers the message with bus.Advanced.TransportMessage.Defer, which defers the original transport message (and thus only needs to include a known header to spot when to bus.Reply)
        [Fact]
        public async Task DeferringRequestDoesNotBreakAbilityToReply_DeferWithTransportMessageApi()
        {
            _service.Handle<string>(async (bus, context, str) =>
            {
                const string deferredMessageHeader = "this message was already deferred";

                if (!context.TransportMessage.Headers.ContainsKey(deferredMessageHeader))
                {
                    var extraHeaders = new Dictionary<string, string>
                    {
                        {deferredMessageHeader, ""},
                    };

                    Console.WriteLine($"SERVICE deferring '{str}' 1 second  (elapsed: {_stopwatch.Elapsed.TotalSeconds:0.# s})");
                    await bus.Advanced.TransportMessage.Defer(TimeSpan.FromSeconds(1), extraHeaders);
                    return;
                }

                const string reply = "yeehaa!";
                Console.WriteLine($"SERVICE replying '{reply}'  (elapsed: {_stopwatch.Elapsed.TotalSeconds:0.# s})");
                await bus.Reply(reply);
            });

            await RunDeferTest();
        }

        async Task RunDeferTest()
        {
            var replyCounter = new SharedCounter(1);

            _client.Handle<string>(async reply =>
            {
                Console.WriteLine($"CLIENT got reply '{reply}'  (elapsed: {_stopwatch.Elapsed.TotalSeconds:0.# s})");
                replyCounter.Decrement();
            });

            await _client.Bus.Send("request");

            replyCounter.WaitForResetEvent();
        }
    }
}