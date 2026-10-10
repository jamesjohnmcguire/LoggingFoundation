/////////////////////////////////////////////////////////////////////////////
// <copyright file="LogServiceTests.cs" company="Digital Zen Works">
// Copyright © 2026 Digital Zen Works.
// </copyright>
/////////////////////////////////////////////////////////////////////////////

namespace LoggingService.Tests;

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Serilog;

/// <summary>
/// Characterizes the logging configuration used by BackupManager.
/// </summary>
[TestFixture]
[NonParallelizable]
internal sealed class LogServiceTests
{
	private string temporaryDirectory = string.Empty;
	private string logPath = string.Empty;

	private Serilog.ILogger previousLogger = null!;
	private TextWriter previousOutput = null!;
	private StringWriter consoleOutput = null!;

	/// <summary>
	/// Isolates process-wide logging and creates a private output directory.
	/// </summary>
	[SetUp]
	public void SetUp()
	{
		string temporaryRoot = Path.GetTempPath();
		Guid identifier = Guid.NewGuid();
		string directoryName = identifier.ToString();

		temporaryDirectory = Path.Combine(
			temporaryRoot, "LoggingService.Tests", directoryName);
		Directory.CreateDirectory(temporaryDirectory);
		logPath = Path.Combine(temporaryDirectory, "BackUp.log");

		previousLogger = Log.Logger;
		previousOutput = Console.Out;

		consoleOutput = new StringWriter(CultureInfo.InvariantCulture);
		Console.SetOut(consoleOutput);
	}

	/// <summary>
	/// Closes file handles, restores globals, and removes only test output.
	/// </summary>
	[TearDown]
	public void TearDown()
	{
		try
		{
			if (!ReferenceEquals(Log.Logger, previousLogger))
			{
				Log.CloseAndFlush();
			}
		}
		finally
		{
			Log.Logger = previousLogger;
			Console.SetOut(previousOutput);

			consoleOutput.Dispose();
			Directory.Delete(temporaryDirectory, recursive: true);
		}
	}

	/// <summary>
	/// Writes to a date-suffixed file and the console.
	/// </summary>
	[Test]
	public void ConfigureWritesToDatedFileAndConsole()
	{
		DateTime before = DateTime.Now;

		LogService.Configure(logPath);

		Log.Information("file-and-console-marker");
		Log.CloseAndFlush();

		DateTime after = DateTime.Now;

		string[] files = Directory.GetFiles(temporaryDirectory, "*.log");
		Assert.That(files, Has.Length.EqualTo(1));

		string fileName = Path.GetFileName(files[0]);
		string[] possibleNames =
		[
			$"BackUp{before:yyyyMMdd}.log",
			$"BackUp{after:yyyyMMdd}.log"
		];
		Assert.That(possibleNames, Does.Contain(fileName));

		bool undatedFileExists = File.Exists(logPath);
		Assert.That(undatedFileExists, Is.False);

		string fileOutput = File.ReadAllText(files[0]);
		string capturedConsoleOutput = consoleOutput.ToString();

		Assert.That(fileOutput, Does.Contain("INF] file-and-console-marker"));
		Assert.That(
			capturedConsoleOutput,
			Does.Contain("INF] file-and-console-marker"));
	}

	/// <summary>
	/// Filters events below Information from both configured destinations.
	/// </summary>
	[Test]
	public void ConfigureFiltersDebugAndVerboseMessages()
	{
		LogService.Configure(logPath);

		Log.Verbose("verbose-marker");
		Log.Debug("debug-marker");
		Log.Information("information-marker");
		Log.CloseAndFlush();

		string fileOutput = ReadLogOutput();
		string capturedConsoleOutput = consoleOutput.ToString();
		string[] outputs = [fileOutput, capturedConsoleOutput];

		foreach (string output in outputs)
		{
			Assert.That(output, Does.Contain("information-marker"));
			Assert.That(output, Does.Not.Contain("debug-marker"));
			Assert.That(output, Does.Not.Contain("verbose-marker"));
		}
	}

	/// <summary>
	/// Preserves earlier messages when logging is closed and reopened.
	/// </summary>
	[Test]
	public void ConfigurePreservesMessagesAfterReopening()
	{
		LogService.Configure(logPath);
		Log.Information("first-session-marker");
		Log.CloseAndFlush();

		LogService.Configure(logPath);
		Log.Information("second-session-marker");
		Log.CloseAndFlush();

		string output = ReadLogOutput();

		Assert.That(output, Does.Contain("first-session-marker"));
		Assert.That(output, Does.Contain("second-session-marker"));
	}

	/// <summary>
	/// Creates missing directories beneath the requested output location.
	/// </summary>
	[Test]
	public void ConfigureCreatesMissingOutputDirectory()
	{
		string nestedPath = Path.Combine(temporaryDirectory, "nested");
		string nestedLogPath = Path.Combine(nestedPath, "BackUp.log");

		LogService.Configure(nestedLogPath);
		Log.Information("nested-directory-marker");
		Log.CloseAndFlush();

		string output = ReadLogOutput();

		Assert.That(output, Does.Contain("nested-directory-marker"));
	}

	/// <summary>
	/// Routes library message helpers through Microsoft logging to Serilog.
	/// </summary>
	/// <param name="method">The helper to exercise.</param>
	/// <param name="level">The expected output level.</param>
	[TestCase("Info", "INF")]
	[TestCase("Information", "INF")]
	[TestCase("Warn", "WRN")]
	[TestCase("Warning", "WRN")]
	[TestCase("Error", "ERR")]
	public void MessageHelpersPreserveLevelAndLiteralText(
		string method, string level)
	{
		LogService.Configure(logPath);

		using ILoggerFactory factory = LoggerFactory.Create(
			ConfigureTestLogging);
		Microsoft.Extensions.Logging.ILogger logger =
			factory.CreateLogger("TestCategory");

		const string message = "literal {braces} marker";

		switch (method)
		{
			case "Info":
				logger.Info(message);
				break;
			case "Information":
				logger.Information(message);
				break;
			case "Warn":
				logger.Warn(message);
				break;
			case "Warning":
				logger.Warning(message);
				break;
			case "Error":
				logger.Error(message);
				break;
			default:
				Assert.Fail("Unknown logging helper.");
				break;
		}

		Log.CloseAndFlush();

		string expected = $"{level}] {message}";
		string fileOutput = ReadLogOutput();
		string capturedConsoleOutput = consoleOutput.ToString();

		Assert.That(fileOutput, Does.Contain(expected));
		Assert.That(capturedConsoleOutput, Does.Contain(expected));
	}

	/// <summary>
	/// Includes exception type and message, or the documented null fallback.
	/// </summary>
	/// <param name="hasException">Whether to supply an exception.</param>
	[TestCase(true)]
	[TestCase(false)]
	public void ExceptionWritesDetailsAtErrorLevel(bool hasException)
	{
		LogService.Configure(logPath);

		using ILoggerFactory factory = LoggerFactory.Create(
			ConfigureTestLogging);
		Microsoft.Extensions.Logging.ILogger logger =
			factory.CreateLogger("TestCategory");

		InvalidOperationException? exception = null;

		if (hasException)
		{
			exception = new InvalidOperationException("exception-marker");
		}

		logger.Exception(exception!);
		Log.CloseAndFlush();

		string expected;

		if (hasException)
		{
			expected = "ERR] System.InvalidOperationException: exception-marker";
		}
		else
		{
			expected = "ERR] Exception occurred";
		}

		string output = ReadLogOutput();

		Assert.That(output, Does.Contain(expected));
	}

	private static void ConfigureTestLogging(ILoggingBuilder builder)
	{
		builder.AddSerilog(Log.Logger);
	}

	private string ReadLogOutput()
	{
		string[] files = Directory.GetFiles(
			temporaryDirectory, "*.log", SearchOption.AllDirectories);
		Assert.That(files, Is.Not.Empty);

		StringBuilder output = new();

		foreach (string file in files)
		{
			string contents = File.ReadAllText(file);
			output.AppendLine(contents);
		}

		return output.ToString();
	}
}
