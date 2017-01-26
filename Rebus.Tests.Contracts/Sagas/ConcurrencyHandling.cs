﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebus.Exceptions;
using Rebus.Sagas;
using Xunit;

namespace Rebus.Tests.Contracts.Sagas
{
    /// <summary>
    /// Test fixture base class for verifying compliance with the <see cref="ISagaStorage"/> contract
    /// </summary>
    public abstract class ConcurrencyHandling<TFactory> : FixtureBase where TFactory : ISagaStorageFactory, new()
    {
        readonly IEnumerable<ISagaCorrelationProperty> _noCorrelationProperties = Enumerable.Empty<ISagaCorrelationProperty>();

        ISagaStorage _sagaStorage;
        TFactory _factory;

        public ConcurrencyHandling()
        {
            _factory = new TFactory();
            _sagaStorage = _factory.GetSagaStorage();
        }

        protected override void TearDown()
        {
            CleanUpDisposables();

            _factory.CleanUp();
        }

        [Fact]
        public async Task ThrowsWhenRevisionDoesNotMatchExpected()
        {
            var id = Guid.NewGuid();

            await _sagaStorage.Insert(new SomeSagaData { Id = id }, _noCorrelationProperties);

            var loadedData1 = await _sagaStorage.Find(typeof(SomeSagaData), "Id", id);

            var loadedData2 = await _sagaStorage.Find(typeof(SomeSagaData), "Id", id);

            await _sagaStorage.Update(loadedData1, _noCorrelationProperties);

            var aggregateException = Assert.Throws<AggregateException>(() => _sagaStorage.Update(loadedData2, _noCorrelationProperties).Wait());

            var baseException = aggregateException.GetBaseException();

            Assert.IsType<ConcurrencyException>(baseException);
        }

        class SomeSagaData : ISagaData
        {
            public Guid Id { get; set; }
            public int Revision { get; set; }
        }
    }
}