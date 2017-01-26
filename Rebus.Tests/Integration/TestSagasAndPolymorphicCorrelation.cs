﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Sagas;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Utilities;
using Rebus.Transport.InMem;
using Xunit;

#pragma warning disable 1998

namespace Rebus.Tests.Integration
{
    public class TestSagasAndPolymorphicCorrelation : FixtureBase
    {
        readonly BuiltinHandlerActivator _activator;

        public TestSagasAndPolymorphicCorrelation()
        {
            _activator = Using(new BuiltinHandlerActivator());

            Configure.With(_activator)
                .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "polycorrrewwllll"))
                .Options(o =>
                {
                    o.SetNumberOfWorkers(1);
                    o.SetMaxParallelism(1);

                    o.SimpleRetryStrategy(maxDeliveryAttempts: 1, secondLevelRetriesEnabled: true);

                    o.LogPipeline(verbose: true);
                })
                .Start();
        }

        [Fact]
        public async Task WorksWithFailedAndInterfacesToo()
        {
            var counter = new SharedCounter(3);

            _activator.Register(() => new SomeSagas(counter));

            await _activator.Bus.SendLocal(new SomeMessageThatFails("bimse"));

            // be sure that the failed message has been processed as an IFailed<SomeMessageThatFails>
            await Task.Delay(2000);

            await _activator.Bus.SendLocal(new Impl("bimse"));
            await _activator.Bus.SendLocal(new Impl("bimse"));

            counter.WaitForResetEvent(100);
        }

        class SomeSagas : Saga<SomeSagaDatas>, IAmInitiatedBy<SomeMessageThatFails>, IAmInitiatedBy<IFailed<SomeMessageThatFails>>, IHandleMessages<IAnInterface>
        {
            readonly SharedCounter _counter;

            public SomeSagas(SharedCounter counter)
            {
                _counter = counter;
            }

            protected override void CorrelateMessages(ICorrelationConfig<SomeSagaDatas> config)
            {
                config.Correlate<SomeMessageThatFails>(m => m.CorrelationId, d => d.CorrelationId);
                config.Correlate<IFailed<SomeMessageThatFails>>(m => m.Message.CorrelationId, d => d.CorrelationId);
                config.Correlate<IAnInterface>(m => m.CorrelationId, d => d.CorrelationId);
            }

            public async Task Handle(SomeMessageThatFails message)
            {
                throw new Exception("bummer dude");
            }

            public async Task Handle(IFailed<SomeMessageThatFails> message)
            {
                Data.CorrelationId = message.Message.CorrelationId;

                _counter.Decrement();
            }

            public async Task Handle(IAnInterface message)
            {
                Data.CorrelationId = message.CorrelationId;

                _counter.Decrement();
            }
        }

        class SomeSagaDatas : ISagaData
        {
            public Guid Id { get; set; }
            public int Revision { get; set; }
            public string CorrelationId { get; set; }
        }

        class SomeMessageThatFails
        {
            public SomeMessageThatFails(string correlationId)
            {
                CorrelationId = correlationId;
            }

            public string CorrelationId { get; }
        }

        interface IAnInterface
        {
            string CorrelationId { get; }
        }

        class Impl : IAnInterface
        {
            public Impl(string correlationId)
            {
                CorrelationId = correlationId;
            }

            public string CorrelationId { get; }
        }

        [Fact]
        public void CanCorrelateWithIncomingMessageWhichIsInherited()
        {
            var encounteredSagaIds = new ConcurrentQueue<Guid>();
            var counter = new SharedCounter(3);

            _activator.Register((bus, context) => new PolySaga(encounteredSagaIds, counter));

            _activator.Bus.SendLocal(new ConcretePolyMessage("blah!")).Wait();
            _activator.Bus.SendLocal(new ConcretePolyMessage("blah!")).Wait();
            _activator.Bus.SendLocal(new ConcretePolyMessage("blah!")).Wait();

            counter.WaitForResetEvent();

            Assert.Equal(1, encounteredSagaIds.Distinct().Count());
        }

        class PolySaga : Saga<PolySagaState>, IAmInitiatedBy<AbstractPolyMessage>
        {
            readonly ConcurrentQueue<Guid> _sagaIdsEncountered;
            readonly SharedCounter _counter;

            public PolySaga(ConcurrentQueue<Guid> sagaIdsEncountered, SharedCounter counter)
            {
                _sagaIdsEncountered = sagaIdsEncountered;
                _counter = counter;
            }

            protected override void CorrelateMessages(ICorrelationConfig<PolySagaState> config)
            {
                config.Correlate<AbstractPolyMessage>(m => m.CorrelationId, d => d.CorrelationId);
            }

            public async Task Handle(AbstractPolyMessage message)
            {
                Data.CorrelationId = message.CorrelationId;

                _sagaIdsEncountered.Enqueue(Data.Id);

                _counter.Decrement();
            }
        }

        class PolySagaState : ISagaData
        {
            public Guid Id { get; set; }
            public int Revision { get; set; }
            public string CorrelationId { get; set; }
        }

        abstract class AbstractPolyMessage
        {
            protected AbstractPolyMessage(string correlationId)
            {
                CorrelationId = correlationId;
            }

            public string CorrelationId { get; }
        }

        class ConcretePolyMessage : AbstractPolyMessage
        {
            public ConcretePolyMessage(string correlationId) : base(correlationId)
            {
            }
        }
    }
}