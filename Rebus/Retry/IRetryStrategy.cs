﻿using Rebus.Pipeline;

namespace Rebus.Retry
{
    /// <summary>
    /// Determines the retry strategy by providing an implementation of <see cref="IRetryStrategyStep"/> which will be
    /// put in front of the incoming message pipeline
    /// </summary>
    public interface IRetryStrategy
    {
        /// <summary>
        /// Should return a <see cref="IRetryStrategyStep"/> which is an <see cref="IIncomingStep"/> that implements the retry strategy
        /// </summary>
        IRetryStrategyStep GetRetryStep();
    }
}