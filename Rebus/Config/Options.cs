﻿using System;

namespace Rebus.Config
{
    /// <summary>
    /// Contains additional options for configuring Rebus internals
    /// </summary>
    public class Options
    {
        /// <summary>
        /// This is the default number of workers that will be started, unless <see cref="NumberOfWorkers"/> is set to something else
        /// </summary>
        public const int DefaultNumberOfWorkers = 1;

        /// <summary>
        /// This is the default number of concurrent asynchrounous operations allowed, unless <see cref="MaxParallelism"/> is set to something else
        /// </summary>
        public const int DefaultMaxParallelism = 5;

        /// <summary>
        /// This is the default timeout for workers to finish running active handlers, unless <see cref="WorkerShutdownTimeout" /> is set to something else.
        /// </summary>
        /// <value>1 minute per default.</value>
        public static readonly TimeSpan DefaultWorkerShutdownTimeout = TimeSpan.FromMinutes(1);

        /// <summary>
        /// This is the default due timeouts poll interval which will be used unless overridde by <see cref="DueTimeoutsPollInterval"/>
        /// </summary>
        public static readonly TimeSpan DefaultDueTimeoutsPollInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Constructs the options with the default settings
        /// </summary>
        public Options()
        {
            NumberOfWorkers = DefaultNumberOfWorkers;
            MaxParallelism = DefaultMaxParallelism;
            DueTimeoutsPollInterval = DefaultDueTimeoutsPollInterval;
            WorkerShutdownTimeout = DefaultWorkerShutdownTimeout;
        }

        /// <summary>
        /// Configures the number of workers. If thread-based workers are used, this is the number of threads that will be created.
        /// This number should be less than or equal to <see cref="MaxParallelism"/>.
        /// </summary>
        public int NumberOfWorkers { get; set; }

        /// <summary>
        /// Configures the total degree of parallelism allowed. This will be the maximum number of parallel potentially asynchrounous operations that can be active,
        /// regardless of the number of workers
        /// </summary>
        public int MaxParallelism { get; set; }

        /// <summary>
        /// Gets/sets the address to use if an external timeout manager is to be used
        /// </summary>
        public string ExternalTimeoutManagerAddressOrNull { get; set; }

        /// <summary>
        /// Gets/sets the poll interval when checking for due timeouts
        /// </summary>
        public TimeSpan DueTimeoutsPollInterval { get; set; }

        /// <summary>
        /// Gets/sets the maximum timeout for workers to finish running active handlers after being signaled to stop.
        /// </summary>
        public TimeSpan WorkerShutdownTimeout { get; set; }
    }
}