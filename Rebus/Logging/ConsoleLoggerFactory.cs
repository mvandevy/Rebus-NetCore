using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rebus.Logging
{
    /// <summary>
    /// Logger factory that logs stuff to the console
    /// </summary>
    public class ConsoleLoggerFactory : AbstractRebusLoggerFactory
    {
        /// <summary>
        /// One single log statement
        /// </summary>
        public class LogStatement
        {
            internal LogStatement(LogLevel level, string text, object[] args, Type type)
            {
                Level = level;
                Args = args;
                Type = type;
                Text = text;
            }

            /// <summary>
            /// The level of this log statement
            /// </summary>
            public LogLevel Level { get; private set; }
            
            /// <summary>
            /// The text (possibly inclusing formatting placeholders) of this log statement
            /// </summary>
            public string Text { get; private set; }
            
            /// <summary>
            /// The values to use for string interpolation
            /// </summary>
            public object[] Args { get; private set; }

            /// <summary>
            /// The type to which this particular logger belongs
            /// </summary>
            public Type Type { get; set; }
        }

        static readonly ConcurrentDictionary<Type, ILog> Loggers = new ConcurrentDictionary<Type, ILog>();

        readonly bool _colored;
        readonly List<Func<LogStatement, bool>> _filters = new List<Func<LogStatement, bool>>(); 

        LoggingColors _colors = new LoggingColors();
        LogLevel _minLevel = LogLevel.Debug;
        bool _showTimestamps;

        /// <summary>
        /// Constructs the logger factory
        /// </summary>
        public ConsoleLoggerFactory(bool colored)
        {
            _colored = colored;
        }

        /// <summary>
        /// Gets or sets the colors to use when logging
        /// </summary>
        public LoggingColors Colors
        {
            get { return _colors; }
            set
            {
                if (value == null)
                {
                    throw new InvalidOperationException("Attempted to set logging colors to null");
                }
                _colors = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum logging level to output to the console
        /// </summary>
        public LogLevel MinLevel
        {
            get { return _minLevel; }
            set
            {
                _minLevel = value;
                Loggers.Clear();
            }
        }

        /// <summary>
        /// Gets the list of filters that each log statement will be passed through in order to evaluate whether
        /// the given log statement should be output to the console
        /// </summary>
        public IList<Func<LogStatement, bool>> Filters
        {
            get { return _filters; }
        }

        /// <summary>
        /// Gets/sets whether timestamps should be shown when logging
        /// </summary>
        public bool ShowTimestamps
        {
            get { return _showTimestamps; }
            set
            {
                _showTimestamps = value;
                Loggers.Clear();
            }
        }

        /// <summary>
        /// Gets a logger for logging stuff from within the specified type
        /// </summary>
        protected override ILog GetLogger(Type type)
        {
            ILog logger;
            
            if (Loggers.TryGetValue(type, out logger)) return logger;
            
            logger = new ConsoleLogger(type, _colors, this, _showTimestamps);
            Loggers.TryAdd(type, logger);
            
            return logger;
        }

        public override ILog GetCurrentClassLogger()
        {
            return GetLogger(typeof(ConsoleLoggerFactory));
        }

        class ConsoleLogger : ILog
        {
            readonly LoggingColors _loggingColors;
            readonly ConsoleLoggerFactory _factory;
            readonly Type _type;
            readonly string _logLineFormatString;

            public ConsoleLogger(Type type, LoggingColors loggingColors, ConsoleLoggerFactory factory, bool showTimestamps)
            {
                _type = type;
                _loggingColors = loggingColors;
                _factory = factory;

                _logLineFormatString = showTimestamps
                                          ? "{0} {1} {2} ({3}): {4}"
                                          : "{1} {2} ({3}): {4}";
            }

            #region ILog Members

            public void Debug(string message, params object[] objs)
            {
                Log(LogLevel.Debug, message, _loggingColors.Debug, objs);
            }

            public void Info(string message, params object[] objs)
            {
                Log(LogLevel.Info, message, _loggingColors.Info, objs);
            }

            public void Warn(string message, params object[] objs)
            {
                Log(LogLevel.Warn, message, _loggingColors.Warn, objs);
            }

            public void Error(Exception exception, string message, params object[] objs)
            {
                Log(LogLevel.Error, string.Format(message, objs) + Environment.NewLine + exception, _loggingColors.Error);
            }

            public void Error(string message, params object[] objs)
            {
                Log(LogLevel.Error, message, _loggingColors.Error, objs);
            }

            #endregion

            void Log(LogLevel level, string message, ColorSetting colorSetting, params object[] objs)
            {
                if (_factory._colored)
                {
                    using (colorSetting.Enter())
                    {
                        Write(level, message, objs);
                    }
                }
                else
                {
                    Write(level, message, objs);
                }
            }

            string LevelString(LogLevel level)
            {
                switch(level)
                {
                    case LogLevel.Debug:
                        return "DEBUG";
                    case LogLevel.Info:
                        return "INFO";
                    case LogLevel.Warn:
                        return "WARN";
                    case LogLevel.Error:
                        return "ERROR";
                    default:
                        throw new ArgumentOutOfRangeException(nameof(level));
                }
            }

            void Write(LogLevel level, string message, object[] objs)
            {
                if ((int)level < (int)_factory.MinLevel) return;
                if (_factory.AbortedByFilter(new LogStatement(level, message, objs, _type))) return;

                var levelString = LevelString(level);

                var threadName = GetThreadName();
                var typeName = _type.FullName;
                try
                {
                    var renderedMessage = string.Format(message, objs);
                    var timeFormat = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");

                    // ReSharper disable EmptyGeneralCatchClause
                    try
                    {
                        Console.WriteLine(_logLineFormatString,
                            timeFormat,
                            typeName,
                            levelString,
                            threadName,
                            renderedMessage);
                    }
                    catch
                    {
                        // nothing to do about it if this part fails   
                    }
                    // ReSharper restore EmptyGeneralCatchClause
                }
                catch
                {
                    Warn("Could not render output string: '{0}' with args: {1}", message, string.Join(", ", objs));
                }
            }

            static string GetThreadName()
            {
                var threadName = Thread.CurrentThread.Name;

                return string.IsNullOrWhiteSpace(threadName)
                    ? $"Thread #{Thread.CurrentThread.ManagedThreadId}"
                    : threadName;
            }
        }

        bool AbortedByFilter(LogStatement logStatement)
        {
            return _filters.Any(f => !f(logStatement));
        }
    }
}