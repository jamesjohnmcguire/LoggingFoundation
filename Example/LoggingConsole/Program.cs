namespace LoggingConsole;

using System;
using ClientLibrary;
using LoggingService;
using Microsoft.Extensions.Logging;

internal class Program
{
	static void Main(string[] args)
	{
		Console.WriteLine("Hello, World!");

		LogService.Initialize("logs/my-custom.log");
		ILogger logger = LogService.CreateLogger("Test Service");
		logger.Information("Frodo");

		TestService testService = new(logger);
		testService.Test();
	}
}
