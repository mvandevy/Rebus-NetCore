﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Rebus.Pipeline
{
    /// <summary>
    /// Decorator of <see cref="IPipeline"/> that can remove steps based on a predicate
    /// </summary>
    public class PipelineStepRemover : IPipeline
    {
        readonly List<Func<IIncomingStep, bool>> _incomingStepPredicates = new List<Func<IIncomingStep, bool>>();
        readonly List<Func<IOutgoingStep, bool>> _outgoingStepPredicates = new List<Func<IOutgoingStep, bool>>();
        readonly IPipeline _pipeline;

        /// <summary>
        /// Constructs the pipeline step remover, wrapping the given pipeline
        /// </summary>
        public PipelineStepRemover(IPipeline pipeline)
        {
            _pipeline = pipeline;
        }

        /// <summary>
        /// Gets the outgoing steps from the wrapped pipeline, unless those where one of the registered outgoing step predicates match
        /// </summary>
        public IEnumerable<IOutgoingStep> SendPipeline()
        {
            foreach (var step in _pipeline.SendPipeline())
            {
                if (HasMatch(step))
                {
                    PossiblyDispose(step);
                    continue;
                }

                yield return step;
            }
        }

        /// <summary>
        /// Gets the incoming steps from the wrapped pipeline, unless those where one of the registered incoming step predicates match
        /// </summary>
        public IEnumerable<IIncomingStep> ReceivePipeline()
        {
            foreach (var step in _pipeline.ReceivePipeline())
            {
                if (HasMatch(step))
                {
                    PossiblyDispose(step);
                    continue;
                }

                yield return step;
            }
        }

        /// <summary>
        /// Adds the predicate, causing matching incoming steps to be removed from the pipeline
        /// </summary>
        public PipelineStepRemover RemoveIncomingStep(Func<IIncomingStep, bool> stepPredicate)
        {
            _incomingStepPredicates.Add(stepPredicate);
            return this;
        }

        /// <summary>
        /// Adds the predicate, causing matching outgoing steps to be removed from the pipeline
        /// </summary>
        public PipelineStepRemover RemoveOutgoingStep(Func<IOutgoingStep, bool> stepPredicate)
        {
            _outgoingStepPredicates.Add(stepPredicate);
            return this;
        }

        bool HasMatch(IOutgoingStep step)
        {
            return _outgoingStepPredicates.Any(p => p(step));
        }

        bool HasMatch(IIncomingStep step)
        {
            return _incomingStepPredicates.Any(p => p(step));
        }

        readonly HashSet<IDisposable> _alreadyDisposedSteps = new HashSet<IDisposable>();

        void PossiblyDispose(IStep step)
        {
            var disposable = step as IDisposable;
            if (disposable == null) return;

            if (_alreadyDisposedSteps.Contains(disposable)) return;

            disposable.Dispose();
            _alreadyDisposedSteps.Add(disposable);
        }
    }
}