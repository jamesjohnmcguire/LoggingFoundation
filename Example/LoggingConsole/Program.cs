/////////////////////////////////////////////////////////////////////////////
// <copyright file="Program.cs" company="Digital Zen Works">
// Copyright © 2026 Digital Zen Works.
// </copyright>
/////////////////////////////////////////////////////////////////////////////

namespace LoggingConsole;

using System;
using ClientLibrary;
using LoggingService;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides the entry point for the application.
/// </summary>
/// <remarks>This class contains the application's main method, which is
/// invoked at startup. It is not intended to be instantiated.</remarks>
internal static class Program
{
	/// <summary>
	/// Serves as the entry point for the application.
	/// </summary>
	/// <param name="args">An array of command-line arguments supplied to the
	/// application.</param>
	internal static void Main(string[] args)
	{
		Console.WriteLine("Hello, World!");

		LogService.Initialize("logs/my-custom.log");
		ILogger logger = LogService.CreateLogger("Test Service");
		logger.Information("Frodo");

		TestService testService = new(logger);
		testService.Test();
	}
}
