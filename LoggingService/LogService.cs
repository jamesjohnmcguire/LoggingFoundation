namespace LoggingService;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Text.Json;
using Serilog;
using Serilog.Core;

public static partial class LogService
{
//	private static readonly ILoggerFactory loggerFactory =
//		LoggerFactory.Create(ConfigureLogging);

	private static ILoggerFactory? loggerFactory;
	private static string logFilePath = "logs/app.log";

	//public static ILogger<T> CreateLogger<T>() =>
	//	loggerFactory.CreateLogger<T>();

	//public static Microsoft.Extensions.Logging.ILogger CreateLogger(
	//	string categoryName) =>
	//	loggerFactory.CreateLogger(categoryName);

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
		string messageTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} " +
			"[{Level:u3}] {Message:lj}{NewLine}{Exception}";

		LoggerConfiguration configuration = new LoggerConfiguration();

		configuration.MinimumLevel.Information();
		configuration.WriteTo.Console(outputTemplate: messageTemplate);
		configuration.WriteTo.File(
			logFilePath,
			rollingInterval: RollingInterval.Day,
			outputTemplate: messageTemplate);

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

	public static ILogger<T> CreateLogger<T>()
	{
		EnsureInitialized();
		return loggerFactory!.CreateLogger<T>();
	}

	public static Microsoft.Extensions.Logging.ILogger CreateLogger(
		string categoryName)
	{
		EnsureInitialized();
		return loggerFactory!.CreateLogger(categoryName);
	}

	private static void EnsureInitialized()
	{
		if (loggerFactory == null)
		{
			throw new InvalidOperationException(
				"LogService must be initialized before use.");
		}
	}

	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Error,
		Message = "{message}")]
	public static partial void Error(
		this Microsoft.Extensions.Logging.ILogger logger,
		string message);

	public static void Info(
		this Microsoft.Extensions.Logging.ILogger logger,
		string message)
	{
		Information(logger, message);
	}

	[LoggerMessage(
		EventId = 3,
		Level = LogLevel.Information,
		Message = "{message}")]
	public static partial void Information(
		this Microsoft.Extensions.Logging.ILogger logger,
		string message);

	public static void Initialize(string filePath)
	{
		logFilePath = filePath;

		loggerFactory = LoggerFactory.Create(ConfigureLogging);
	}

	public static void Warn(
		this Microsoft.Extensions.Logging.ILogger logger,
		string message)
	{
		Warning(logger, message);
	}

	[LoggerMessage(
		EventId = 2,
		Level = LogLevel.Warning,
		Message = "{message}")]
	public static partial void Warning(
		this Microsoft.Extensions.Logging.ILogger logger,
		string message);
}
