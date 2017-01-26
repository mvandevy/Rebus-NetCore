﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Rebus.Extensions;
using Rebus.Injection;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Timeouts;

namespace Rebus.Config
{
    /// <summary>
    /// Allows for configuring additional options
    /// </summary>
    public class OptionsConfigurer
    {
        readonly Options _options;
        readonly Injectionist _injectionist;

        internal OptionsConfigurer(Options options, Injectionist injectionist)
        {
            _options = options;
            _injectionist = injectionist;
        }

        internal StandardConfigurer<TService> GetConfigurer<TService>()
        {
            return new StandardConfigurer<TService>(_injectionist, _options);
        }

        /// <summary>
        /// Configures the number of workers to start competing over the input queue
        /// </summary>
        public void SetNumberOfWorkers(int numberOfWorkers)
        {
            _options.NumberOfWorkers = numberOfWorkers;
        }

        /// <summary>
        /// Configures the total degree of parallelism allowed. This will be the maximum number of parallel potentially asynchrounous operations that can be active,
        /// regardless of the number of workers
        /// </summary>
        public void SetMaxParallelism(int maxParallelism)
        {
            _options.MaxParallelism = maxParallelism;
        }

        /// <summary>
        /// Configures the maximum timeout for workers to finish running active handlers after being signaled to stop.
        /// </summary>
        public void SetWorkerShutdownTimeout(TimeSpan timeout)
        {
            _options.WorkerShutdownTimeout = timeout;
        }

        /// <summary>
        /// Configures the interval between polling the endpoint's configured <see cref="ITimeoutManager"/> for due timeouts.
        /// Defaults to <see cref="Options.DefaultDueTimeoutsPollInterval"/>
        /// </summary>
        public void SetDueTimeoutsPollInteval(TimeSpan dueTimeoutsPollInterval)
        {
            _options.DueTimeoutsPollInterval = dueTimeoutsPollInterval;
        }

        /// <summary>
        /// Registers the given factory function as a resolver of the given primary implementation of the <typeparamref name="TService"/> service
        /// </summary>
        public void Register<TService>(Func<IResolutionContext, TService> resolverMethod, string description = null)
        {
            _injectionist.Register(resolverMethod, description);
        }

        /// <summary>
        /// Gets whether a primary implementation resolver has been registered for the <typeparamref name="TService"/> service
        /// </summary>
        public bool Has<TService>()
        {
            return _injectionist.Has<TService>();
        }

        /// <summary>
        /// Registers the given factory function as a resolve of the given decorator of the <typeparamref name="TService"/> service
        /// </summary>
        public void Decorate<TService>(Func<IResolutionContext, TService> resolverMethod, string description = null)
        {
            _injectionist.Decorate(resolverMethod, description);
        }

        /// <summary>
        /// Outputs the layout of the send and receive pipelines to the log
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void LogPipeline(bool verbose = false)
        {
            // when the pipeline is resolved, we hook ourselves in and log it!
            _injectionist.ResolveRequested += serviceType =>
            {
                if (serviceType != typeof (IPipeline)) return;

                _injectionist.Decorate(c =>
                {
                    var pipeline = c.Get<IPipeline>();
                    var logger = c.Get<IRebusLoggerFactory>().GetLogger<OptionsConfigurer>();

                    var receivePipeline = pipeline.ReceivePipeline();
                    var sendPipeline = pipeline.SendPipeline();

                    logger.Info(@"
------------------------------------------------------------------------------
Message pipelines
------------------------------------------------------------------------------
Send pipeline:
{0}

Receive pipeline:
{1}
------------------------------------------------------------------------------
", Format(sendPipeline, verbose), Format(receivePipeline, verbose));


                    return pipeline;
                });
            };
        }

        static string Format(IEnumerable<IStep> pipeline, bool verbose)
        {
            return string.Join(Environment.NewLine,
                pipeline.Select((step, i) =>
                {
                    var stepType = step.GetType().FullName;
                    var stepString = $"    {stepType}";

                    if (verbose)
                    {
                        var docs = GetDocsOrNull(step);

                        if (!string.IsNullOrWhiteSpace(docs))
                        {
                            stepString = string.Concat(stepString, Environment.NewLine, docs.WrappedAt(60).Indented(8),
                                Environment.NewLine);
                        }

                    }

                    return stepString;
                }));
        }

        static string GetDocsOrNull(IStep step)
        {
            var docsAttribute = step.GetType()
                .GetTypeInfo().GetCustomAttributes()
                .OfType<StepDocumentationAttribute>()
                .FirstOrDefault();

            return docsAttribute?.Text;
        }
    }
}