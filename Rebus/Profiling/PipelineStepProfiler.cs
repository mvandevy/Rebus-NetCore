﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebus.Pipeline;

namespace Rebus.Profiling
{
    /// <summary>
    /// Implementation of <see cref="IPipeline"/> that wraps another <see cref="IPipeline"/>
    /// and injects instances of a single step into the pipeline which can be used to measure time spent
    /// </summary>
    public class PipelineStepProfiler : IPipeline
    {
        readonly IPipeline _pipeline;
        readonly PipelineStepProfilerStats _profilerStats;

        /// <summary>
        /// Creates the profiler
        /// </summary>
        public PipelineStepProfiler(IPipeline pipeline, PipelineStepProfilerStats profilerStats)
        {
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));
            if (profilerStats == null) throw new ArgumentNullException(nameof(profilerStats));
            _pipeline = pipeline;
            _profilerStats = profilerStats;
        }

        /// <summary>
        /// Gets the original send pipeline
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IOutgoingStep> SendPipeline()
        {
            return _pipeline.SendPipeline();
        }

        /// <summary>
        /// Gets a pipeline with time-tracking steps interleaved
        /// </summary>
        public IEnumerable<IIncomingStep> ReceivePipeline()
        {
            yield return new RegisterCollectedProfilerStatsStep(_profilerStats);

            foreach (var step in _pipeline.ReceivePipeline())
            {
                yield return new ProfilerStep(step);
                yield return step;
            }
        }

        [StepDocumentation("Collected profiler measurements and registers them with the stats collector")]
        class RegisterCollectedProfilerStatsStep : IIncomingStep
        {
            readonly PipelineStepProfilerStats _profilerStats;

            public RegisterCollectedProfilerStatsStep(PipelineStepProfilerStats profilerStats)
            {
                _profilerStats = profilerStats;
            }

            public async Task Process(IncomingStepContext context, Func<Task> next)
            {
                var statsContext = new StatsContext();

                // save stats context for all the ProfilerSteps to find
                context.Save(statsContext);

                await next();

                _profilerStats.Register(statsContext);
            }
        }

        [StepDocumentation("Measures time spent in the rest of the pipeline")]
        class ProfilerStep : IIncomingStep
        {
            readonly IIncomingStep _nextStep;

            public ProfilerStep(IIncomingStep nextStep)
            {
                _nextStep = nextStep;
            }

            public async Task Process(IncomingStepContext context, Func<Task> next)
            {
                var statsContext = context.Load<StatsContext>();

                using (statsContext.Measure(_nextStep))
                {
                    await next();
                }
            }
        }
    }
}