﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Tests.Contracts.Extensions;
using Rebus.Transport;
using Rebus.Transport.InMem;
using Xunit;

#pragma warning disable 1998

namespace Rebus.Tests.Contracts.Activation
{
    public abstract class ContainerTests<TFactory> : FixtureBase where TFactory : IContainerAdapterFactory, new()
    {
        TFactory _factory;

        protected ContainerTests() : base()
        {
            _factory = new TFactory();

            DisposableHandler.Reset();
            SomeHandler.Reset();
            StaticHandler.Reset();
        }

        class StaticHandler : IHandleMessages<StaticHandlerMessage>
        {
            public static readonly ConcurrentQueue<object> HandledMessages = new ConcurrentQueue<object>();

            public async Task Handle(StaticHandlerMessage message)
            {
                HandledMessages.Enqueue(message);
            }

            public static void Reset()
            {
                object obj;
                while (HandledMessages.TryDequeue(out obj)) ;
            }
        }

        class StaticHandlerMessage
        {
            public StaticHandlerMessage(string text)
            {
                Text = text;
            }

            public string Text { get; }
        }

        [Fact]
        public void IntegrationTest()
        {
            _factory.RegisterHandlerType<StaticHandler>();

            var activator = _factory.GetActivator();

            Configure.With(activator)
                .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "container-integration-test"))
                .Start();

            var bus = _factory.GetBus();

            bus.SendLocal(new StaticHandlerMessage("hej med dig")).Wait();

            Thread.Sleep(2000);

            Assert.Equal("hej med dig", StaticHandler.HandledMessages.Cast<StaticHandlerMessage>().Single().Text);
        }

        [Fact, Description("Some container adapters were implemented in a way that would double-resolve handlers because of lazy evaluation of an IEnumerable")]
        public void DoesNotDoubleResolveBecauseOfLazyEnumerableEvaluation()
        {
            _factory.RegisterHandlerType<SomeHandler>();
            var handlerActivator = _factory.GetActivator();

            using (var context = new DefaultTransactionContext())
            {
                var handlers = handlerActivator.GetHandlers("hej", context).Result.ToList();

                //context.Complete().Wait();
            }

            var createdInstances = SomeHandler.CreatedInstances.ToList();
            Assert.Equal(new[] { 0 }, createdInstances);

            var disposedInstances = SomeHandler.DisposedInstances.ToList();
            Assert.Equal(new[] { 0 }, disposedInstances);
        }

        class SomeHandler : IHandleMessages<string>, IDisposable
        {
            public static readonly ConcurrentQueue<int> CreatedInstances = new ConcurrentQueue<int>();
            public static readonly ConcurrentQueue<int> DisposedInstances = new ConcurrentQueue<int>();

            static int _instanceIdCounter;
            readonly int _instanceId = _instanceIdCounter++;

            public SomeHandler()
            {
                CreatedInstances.Enqueue(_instanceId);
            }

            public Task Handle(string message)
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                DisposedInstances.Enqueue(_instanceId);
            }

            public static void Reset()
            {
                while (DisposedInstances.Count > 0)
                {
                    int temp;
                    DisposedInstances.TryDequeue(out temp);
                }
            }
        }

        [Fact]
        public void CanGetDecoratedBus()
        {
            var busReturnedFromConfiguration = Configure.With(_factory.GetActivator())
                .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "decorated-bus-test"))
                .Options(o => o.Decorate<IBus>(c => new TestBusDecorator(c.Get<IBus>())))
                .Start();

            var busReturnedFromContainer = _factory.GetBus();

            Assert.IsType<TestBusDecorator>(busReturnedFromConfiguration);
            Assert.IsType<TestBusDecorator>(busReturnedFromContainer);
        }

        class TestBusDecorator : IBus
        {
            readonly IBus _bus;

            public TestBusDecorator(IBus bus)
            {
                _bus = bus;
            }

            public void Dispose()
            {
                _bus.Dispose();
            }

            public Task SendLocal(object commandMessage, Dictionary<string, string> optionalHeaders = null)
            {
                return _bus.SendLocal(commandMessage, optionalHeaders);
            }

            public Task Send(object commandMessage, Dictionary<string, string> optionalHeaders = null)
            {
                return _bus.SendLocal(commandMessage, optionalHeaders);
            }

            public Task Reply(object replyMessage, Dictionary<string, string> optionalHeaders = null)
            {
                return _bus.Reply(replyMessage, optionalHeaders);
            }

            public Task Defer(TimeSpan delay, object message, Dictionary<string, string> optionalHeaders = null)
            {
                return _bus.Defer(delay, message, optionalHeaders);
            }

            public IAdvancedApi Advanced
            {
                get { return _bus.Advanced; }
            }

            public Task Subscribe<TEvent>()
            {
                return _bus.Subscribe<TEvent>();
            }

            public Task Subscribe(Type eventType)
            {
                return _bus.Subscribe(eventType);
            }

            public Task Unsubscribe<TEvent>()
            {
                return _bus.Unsubscribe<TEvent>();
            }

            public Task Unsubscribe(Type eventType)
            {
                return _bus.Unsubscribe(eventType);
            }

            public Task Publish(object eventMessage, Dictionary<string, string> optionalHeaders = null)
            {
                return _bus.Publish(eventMessage, optionalHeaders);
            }
        }

        [Fact]
        public void CanSetBusAndDisposeItAfterwards()
        {
            var factoryForThisTest = new TFactory();
            var fakeBus = new FakeBus();

            try
            {
                var activator = factoryForThisTest.GetActivator();

                if (activator is IContainerAdapter)
                {
                    ((IContainerAdapter)activator).SetBus(fakeBus);
                }
            }
            finally
            {
                factoryForThisTest.CleanUp();
            }

            Assert.True(fakeBus.Disposed,"The disposable bus instance was NOT disposed when the container was disposed");
        }

        class FakeBus : IBus
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }

            public Task SendLocal(object commandMessage, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public Task Send(object commandMessage, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public Task Reply(object replyMessage, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public Task Publish(string topic, object eventMessage, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public Task Defer(TimeSpan delay, object message, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public Task Subscribe(string topic)
            {
                throw new NotImplementedException();
            }

            public Task Unsubscribe(string topic)
            {
                throw new NotImplementedException();
            }

            public Task Route(string destinationAddress, object explicitlyRoutedMessage, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public IAdvancedApi Advanced { get; private set; }

            public Task Subscribe<TEvent>()
            {
                throw new NotImplementedException();
            }

            public Task Subscribe(Type eventType)
            {
                throw new NotImplementedException();
            }

            public Task Unsubscribe<TEvent>()
            {
                throw new NotImplementedException();
            }

            public Task Unsubscribe(Type eventType)
            {
                throw new NotImplementedException();
            }

            public Task Publish(object eventMessage, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public async Task ResolvesHandlersPolymorphically()
        {
            _factory.RegisterHandlerType<BaseMessageHandler>();

            var handlerActivator = _factory.GetActivator();

            using (var transactionContext = new DefaultTransactionContext())
            {
                var handlers = (await handlerActivator.GetHandlers(new DerivedMessage(), transactionContext)).ToList();

                Assert.Equal(1, handlers.Count);
                Assert.IsType<BaseMessageHandler>(handlers[0]);
            }
        }

        abstract class BaseMessage { }
        class DerivedMessage : BaseMessage { }
        class BaseMessageHandler : IHandleMessages<BaseMessage> { public async Task Handle(BaseMessage message) { } }

        [Fact]
        public async Task ResolvingWithoutRegistrationYieldsEmptySequenec()
        {
            var handlerActivator = _factory.GetActivator();

            using (var transactionContext = new DefaultTransactionContext())
            {
                var handlers = (await handlerActivator.GetHandlers("hej", transactionContext)).ToList();

                Assert.Equal(0, handlers.Count);
            }
        }

        [Fact]
        public async Task CanRegisterHandler()
        {
            _factory.RegisterHandlerType<SomeStringHandler>();
            var handlerActivator = _factory.GetActivator();

            using (var transactionContext = new DefaultTransactionContext())
            {
                var handlers = (await handlerActivator.GetHandlers("hej", transactionContext)).ToList();

                Assert.Equal(1, handlers.Count);
                Assert.IsType<SomeStringHandler>(handlers[0]);
            }
        }

        [Fact]
        public async Task ResolvedHandlerIsDisposed()
        {
            _factory.RegisterHandlerType<DisposableHandler>();

            var bus = Configure.With(_factory.GetActivator())
                .Transport(t => t.UseInMemoryTransport(new InMemNetwork(true), "somequeue"))
                .Start();

            Using(bus);

            await bus.SendLocal("hej med dig");

            await DisposableHandler.Events.WaitUntil(c => c.Count == 2);

            Assert.True(DisposableHandler.WasCalledAllright, "The handler was apparently not called");
            Assert.True(DisposableHandler.WasDisposedAllright, "The handler was apparently not disposed");
        }

        class SomeStringHandler : IHandleMessages<string>
        {
            public async Task Handle(string message)
            {
            }
        }

        class DisposableHandler : IHandleMessages<string>, IDisposable
        {
            public static ConcurrentQueue<string> Events { get; set; }

            public static bool WasCalledAllright { get; private set; }

            public static bool WasDisposedAllright { get; private set; }

            public async Task Handle(string message)
            {
                WasCalledAllright = true;

                Events.Enqueue($"handled {message}");
            }

            public void Dispose()
            {
                WasDisposedAllright = true;

                Events.Enqueue("disposed");
            }

            public static void Reset()
            {
                WasCalledAllright = false;
                WasDisposedAllright = false;
                Events = new ConcurrentQueue<string>();
            }
        }
    }
}