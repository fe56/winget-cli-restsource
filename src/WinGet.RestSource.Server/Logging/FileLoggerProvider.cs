namespace Microsoft.WinGet.RestSource.Server.Logging
{
    using System.Globalization;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Registers a logger that appends to one file.
    /// </summary>
    internal static class FileLoggerExtensions
    {
        /// <summary>
        /// Adds a file logger. Creates the parent directory when it is missing.
        /// </summary>
        /// <param name="builder">Logging builder.</param>
        /// <param name="path">Log file path.</param>
        /// <returns>The same builder.</returns>
        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            builder.AddProvider(new FileLoggerProvider(path));
            return builder;
        }
    }

    /// <summary>
    /// Writes log lines to a shared append-only file.
    /// </summary>
    internal sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly StreamWriter writer;
        private readonly object gate = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLoggerProvider"/> class.
        /// </summary>
        /// <param name="path">Log file path.</param>
        public FileLoggerProvider(string path)
        {
            var stream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);
            this.writer = new StreamWriter(stream) { AutoFlush = true };
        }

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, this.writer, this.gate);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.writer.Dispose();
        }
    }

    /// <summary>
    /// Formats one category's log entries.
    /// </summary>
    internal sealed class FileLogger : ILogger
    {
        private readonly string categoryName;
        private readonly StreamWriter writer;
        private readonly object gate;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLogger"/> class.
        /// </summary>
        /// <param name="categoryName">Logger category.</param>
        /// <param name="writer">Shared writer.</param>
        /// <param name="gate">Lock shared by every logger from the provider.</param>
        public FileLogger(string categoryName, StreamWriter writer, object gate)
        {
            this.categoryName = categoryName;
            this.writer = writer;
            this.gate = gate;
        }

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        /// <inheritdoc/>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!this.IsEnabled(logLevel))
            {
                return;
            }

            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
            var line = $"{timestamp} [{logLevel}] {this.categoryName}: {formatter(state, exception)}";

            lock (this.gate)
            {
                try
                {
                    this.writer.WriteLine(line);
                    if (exception != null)
                    {
                        this.writer.WriteLine(exception);
                    }
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }
}
