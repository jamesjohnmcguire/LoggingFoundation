/////////////////////////////////////////////////////////////////////////////
// <copyright file="LogService.cs" company="Digital Zen Works">
// Copyright © 2026 Digital Zen Works.
// </copyright>
/////////////////////////////////////////////////////////////////////////////

namespace LoggingService;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Provides static methods for creating and managing loggers, as well as
/// logging messages at various severity levels within the application.
/// </summary>
/// <remarks>Callers must initialize the logging system by invoking the
/// Initialize method before creating or using loggers. This class supports
/// logging to both the console and file outputs, and integrates with
/// Microsoft.Extensions.Logging and Serilog. All members are thread-safe and
/// intended for use throughout the application's lifetime.</remarks>
public static partial class LogService
{
	private static ILoggerFactory? loggerFactory;
	private static string logFilePath = "logs/app.log";

	/// <summary>
	/// Configures the logging system to write informational and higher-level
	/// log events to both the console and a rolling log file.
	/// </summary>
	/// <remarks>This method sets up logging with a standard output template
	/// and ensures that log events at the Information level or higher are
	/// captured. The log file will be created if it does not exist and will
	/// roll over at midnight each day. Calling this method multiple times will
	/// overwrite the existing logger configuration.</remarks>
	/// <param name="logFilePath">The file path where log entries will be
	/// written. The log file will roll over daily. Cannot be null or empty.
	/// </param>
	public static void Configure(string logFilePath)
	{
		const string outputTemplate =
			"[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] " +
			"{Message:lj}{NewLine}{Exception}";

		LoggerConfiguration configuration = new();
		configuration.MinimumLevel.Information();

		LoggerSinkConfiguration sinkConfiguration = configuration.WriteTo;
		sinkConfiguration.Console(
			LogEventLevel.Information,
			outputTemplate,
			CultureInfo.CurrentCulture);
		sinkConfiguration.File(
			logFilePath,
			LogEventLevel.Information,
			outputTemplate,
			CultureInfo.CurrentCulture,
			rollingInterval: RollingInterval.Day);

		Log.Logger = configuration.CreateLogger();
	}

	/// <summary>
	/// Creates a logger instance for the specified category type.
	/// </summary>
	/// <typeparam name="T">The category type for which to create a logger.
	/// Typically, this is the class or component that will use the logger.
	/// </typeparam>
	/// <returns>An <see cref="ILogger{T}"/> instance that can be used to log
	/// messages for the specified category type.</returns>
	public static ILogger<T> CreateLogger<T>()
	{
		EnsureInitialized();
		return loggerFactory!.CreateLogger<T>();
	}

	/// <summary>
	/// Creates a new logger instance for the specified category name.
	/// </summary>
	/// <param name="categoryName">The name of the category for messages
	/// produced by the logger. This value is typically the fully qualified
	/// class name or another logical grouping. Cannot be null or empty.
	/// </param>
	/// <returns>An ILogger instance that can be used to log messages for the
	/// specified category.</returns>
	public static Microsoft.Extensions.Logging.ILogger CreateLogger(
		string categoryName)
	{
		EnsureInitialized();
		return loggerFactory!.CreateLogger(categoryName);
	}

	/// <summary>
	/// Logs an error message with the specified logger at the Error level.
	/// </summary>
	/// <remarks>Use this method to record error events that indicate a failure
	/// in the current operation. The message can include contextual
	/// information to aid in diagnosing issues.</remarks>
	/// <param name="logger">The logger instance used to write the error
	/// message. Cannot be null.</param>
	/// <param name="message">The error message to log. Cannot be null or
	/// empty.</param>
	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Error,
		Message = "{message}")]
	public static partial void Error(
		this Microsoft.Extensions.Logging.ILogger logger,
		string message);

	/// <summary>
	/// Logs the specified exception as an error using the provided logger.
	/// </summary>
	/// <param name="logger">The logger instance used to record the error.
	/// Cannot be null.</param>
	/// <param name="exception">The exception to log. If null, a generic error
	/// message is logged instead.</param>
	public static void Exception(
		this Microsoft.Extensions.Logging.ILogger logger,
		Exception exception)
	{
		string details = "Exception occurred";

		if (exception != null)
		{
			details = exception.ToString();
		}

		Error(logger, details);
	}

	/// <summary>
	/// Logs an exception with contextual information about the caller and
	/// line number.
	/// </summary>
	/// <remarks>This method is intended to provide additional context when
	/// logging exceptions by including the caller's name and line number. It
	/// is useful for debugging and tracing errors in the codebase.</remarks>
	/// <param name="logger">The logger instance used to write the exception
	/// details.</param>
	/// <param name="exception">The exception to be logged. Cannot be null.
	/// </param>
	/// <param name="caller">The name of the calling member. This value is
	/// automatically provided by the compiler and should not be set explicitly.
	/// </param>
	/// <param name="lineNumber">The line number in the source file at which
	/// the method is called. This value is automatically provided by the
	/// compiler and should not be set explicitly.</param>
	public static void Exception(
		this Microsoft.Extensions.Logging.ILogger logger,
		Exception exception,
		[CallerMemberName] string caller = "",
		[CallerLineNumber] int lineNumber = 0)
	{
		string message = "Exception in {caller} at line {lineNumber}";
		Error(logger, message);

		Exception(logger, exception);
	}

	/// <summary>
	/// Writes an informational log message using the specified logger.
	/// </summary>
	/// <remarks>This extension method provides a convenient way to log
	/// informational messages using the ILogger interface. The message is
	/// logged with the Information log level.</remarks>
	/// <param name="logger">The logger instance used to write the
	/// informational message. Cannot be null.</param>
	/// <param name="message">The message to log at the informational level.
	/// Cannot be null.</param>
	public static void Info(
		this Microsoft.Extensions.Logging.ILogger logger,
		string message)
	{
		Information(logger, message);
	}

	/// <summary>
	/// Writes an informational log message using the specified logger.
	/// </summary>
	/// <remarks>Use this method to record general information about
	/// application flow or state. Informational logs are typically used for
	/// tracking normal operations and are not intended for error or diagnostic
	/// details.</remarks>
	/// <param name="logger">The logger instance used to write the log entry.
	/// Cannot be null.</param>
	/// <param name="message">The message to log. This value can be a composite
	/// format string.</param>
	[LoggerMessage(
		EventId = 3,
		Level = LogLevel.Information,
		Message = "{message}")]
	public static partial void Information(
		this Microsoft.Extensions.Logging.ILogger logger,
		string message);

	/// <summary>
	/// Initializes the logging system with the specified log file path.
	/// </summary>
	/// <remarks>Call this method before performing any logging operations to
	/// ensure that log messages are written to the correct file. Subsequent
	/// calls will reconfigure the logging system with the new file path.
	/// </remarks>
	/// <param name="filePath">The full path to the log file to which log
	/// entries will be written. Cannot be null or empty.</param>
	public static void Initialize(string filePath)
	{
		logFilePath = filePath;

		loggerFactory = LoggerFactory.Create(ConfigureLogging);
	}

	/// <summary>
	/// Writes a warning log message using the specified logger.
	/// </summary>
	/// <param name="logger">The logger instance used to write the warning
	/// message. Cannot be null.</param>
	/// <param name="message">The message to log as a warning. Cannot be null.
	/// </param>
	public static void Warn(
		this Microsoft.Extensions.Logging.ILogger logger,
		string message)
	{
		Warning(logger, message);
	}

	/// <summary>
	/// Writes a warning log message using the specified logger.
	/// </summary>
	/// <remarks>Use this method to log warning messages that highlight
	/// potential issues or unexpected events in the application's flow. The
	/// message is logged with a warning severity and may be filtered based on
	/// the logger's configuration.</remarks>
	/// <param name="logger">The logger instance used to write the warning
	/// message. Cannot be null.</param>
	/// <param name="message">The message to log. This value can include format
	/// placeholders.</param>
	[LoggerMessage(
		EventId = 2,
		Level = LogLevel.Warning,
		Message = "{message}")]
	public static partial void Warning(
		this Microsoft.Extensions.Logging.ILogger logger,
		string message);

	private static void ConfigureConsoleLogging(
		SimpleConsoleFormatterOptions options)
	{
		options.IncludeScopes = false;
		options.SingleLine = true;
	}

	private static void ConfigureJsonLogging(
		JsonConsoleFormatterOptions options)
	{
		JsonWriterOptions writerOption = new()
		{
			Indented = true
		};

		options.JsonWriterOptions = writerOption;
	}

	private static void ConfigureSerilogLogging(ILoggingBuilder builder)
	{
		const string outputTemplate =
			"[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] " +
			"{Message:lj}{NewLine}{Exception}";

		LoggerConfiguration configuration = new();
		configuration.MinimumLevel.Information();

		LoggerSinkConfiguration sinkConfiguration = configuration.WriteTo;
		sinkConfiguration.Console(
			LogEventLevel.Information,
			outputTemplate,
			CultureInfo.CurrentCulture);
		sinkConfiguration.File(
			logFilePath,
			LogEventLevel.Information,
			outputTemplate,
			CultureInfo.CurrentCulture,
			rollingInterval: RollingInterval.Day);

		Logger serilogLogger = configuration.CreateLogger();

		builder.AddSerilog(serilogLogger);
	}

	private static void ConfigureLogging(ILoggingBuilder builder)
	{
#if CONSOLE_ONLY
		builder.AddSimpleConsole(ConfigureConsoleLogging);
#endif
		ConfigureSerilogLogging(builder);
	}

	private static void EnsureInitialized()
	{
		if (loggerFactory == null)
		{
			throw new InvalidOperationException(
				"LogService must be initialized before use.");
		}
	}
}
