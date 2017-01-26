﻿using System;

namespace Rebus.Pipeline
{
    /// <summary>
    /// Documents the purpose of an <see cref="IIncomingStep"/> or <see cref="IOutgoingStep"/> which can then be used by tools to generate nice docs
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class StepDocumentationAttribute : Attribute
    {
        /// <summary>
        /// Creates the attribute with the given documentation test
        /// </summary>
        public StepDocumentationAttribute(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            Text = text;
        }

        /// <summary>
        /// Gets the documentation text
        /// </summary>
        public string Text { get; private set; }
    }
}