﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Messages;
using Rebus.Retry.Simple;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Rebus.Tests.Contracts.Utilities;
using Rebus.Transport.InMem;
using Xunit;

#pragma warning disable 1998

namespace Rebus.Tests.Integration
{
    public class TestSecondLevelRetries : FixtureBase
    {
        const string InputQueueName = "2nd level goodness";
        BuiltinHandlerActivator _activator;
        IBus _bus;
        InMemNetwork _network;

        public TestSecondLevelRetries()
        {
            _activator = Using(new BuiltinHandlerActivator());

            _network = new InMemNetwork();

            _bus = Configure.With(_activator)
                .Transport(t => t.UseInMemoryTransport(_network, InputQueueName))
                .Options(o => o.SimpleRetryStrategy(secondLevelRetriesEnabled: true))
                .Start();
        }

        [Fact]
        public async Task ItWorksWithCovarianceToo()
        {
            var counter = new SharedCounter(1);

            Using(counter);

            _activator.Handle<BaseMessage>(async baseMessage =>
            {
                throw new Exception("1st level!!");
            });

            _activator.Handle<IFailed<BaseMessage>>(async failed =>
            {
                if (failed.Message is ConcreteMessage)
                {
                    counter.Decrement();
                    return;
                }

                counter.Fail("Did not receive the expected message!");
            });

            await _bus.SendLocal(new ConcreteMessage());

            counter.WaitForResetEvent();
        }

        abstract class BaseMessage { }

        class ConcreteMessage : BaseMessage { }

        [Fact]
        public async Task ItWorks()
        {
            var counter = new SharedCounter(1);

            Using(counter);

            _activator.Handle<string>(async str =>
            {
                throw new Exception("1st level!!");
            });

            _activator.Handle<IFailed<string>>(async failed =>
            {
                if (failed.Message != "hej med dig!")
                {
                    counter.Fail("Did not receive the expected message!");
                    return;
                }

                counter.Decrement();
            });

            await _bus.SendLocal("hej med dig!");

            counter.WaitForResetEvent();
        }

        [Fact]
        public async Task IncludesFullErrorDetailsWhenSecondLevelRetriesFailToo()
        {
            var counter = new SharedCounter(1);

            Using(counter);

            _activator.Handle<string>(async str =>
            {
                throw new Exception("1st level!!");
            });

            _activator.Handle<IFailed<string>>(async failed =>
            {
                throw new Exception("2nd level!!");
            });

            await _bus.SendLocal("hej med dig!");

            var transportMessage = await _network.WaitForNextMessageFrom("error");
            var errorDetails = transportMessage.Headers[Headers.ErrorDetails];

            Console.WriteLine(errorDetails);

            Assert.Contains("1st level!!", errorDetails);
            Assert.Contains("2nd level!!", errorDetails);
        }

        [Fact]
        public async Task StillWorksWhenIncomingMessageCannotBeDeserialized()
        {
            const string brokenJsonString = @"{'broken': 'json', // DIE!!1}";

            var headers = new Dictionary<string, string>
            {
                {Headers.MessageId, Guid.NewGuid().ToString()},
                {Headers.ContentType, "application/json;charset=utf-8"},
            };
            var body = Encoding.UTF8.GetBytes(brokenJsonString);
            var transportMessage = new TransportMessage(headers, body);
            var inMemTransportMessage = new InMemTransportMessage(transportMessage);
            _network.Deliver(InputQueueName, inMemTransportMessage);

            var failedMessage = await _network.WaitForNextMessageFrom("error");

            Assert.NotNull(failedMessage);
            var bodyString = Encoding.UTF8.GetString(failedMessage.Body);
            Assert.Equal(brokenJsonString, bodyString);
        }

        [Fact]
        public async Task FailedMessageAllowsForAccessingHeaders()
        {
            var counter = new SharedCounter(1);

            Using(counter);

            _activator.Handle<string>(async str =>
            {
                throw new Exception("1st level!!");
            });

            var headersOfFailedMessage = new Dictionary<string, string>();

            _activator.Handle<IFailed<string>>(async failed =>
            {
                if (failed.Message != "hej med dig!")
                {
                    counter.Fail("Did not receive the expected message!");
                    return;
                }

                foreach (var kvp in failed.Headers)
                {
                    headersOfFailedMessage.Add(kvp.Key, kvp.Value);
                }

                Console.WriteLine();
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("ERROR DESCRIPTION:");
                Console.WriteLine();
                Console.WriteLine(failed.ErrorDescription);
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("CAUGHT EXCEPTIONS:");
                Console.WriteLine();
                Console.WriteLine(string.Join(Environment.NewLine + Environment.NewLine, failed.Exceptions));
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine();

                counter.Decrement();
            });

            var headers = new Dictionary<string, string>
            {
                {"custom-header", "with-a-custom-value" }
            };

            await _bus.SendLocal("hej med dig!", headers);

            counter.WaitForResetEvent();

            Console.WriteLine(string.Join(Environment.NewLine, headersOfFailedMessage.Select(kvp =>
               $"    {kvp.Key}: {kvp.Value}")));

            Assert.Equal("with-a-custom-value", headersOfFailedMessage["custom-header"]);
        }
    }
}