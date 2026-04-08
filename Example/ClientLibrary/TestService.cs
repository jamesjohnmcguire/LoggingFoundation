namespace ClientLibrary;

using System;
using System.Collections.Generic;
using System.Text;

using LoggingService;
using Microsoft.Extensions.Logging;

public class TestService
{
	private readonly ILogger logger;

	public TestService(ILogger logger)
	{
		this.logger = logger;
	}

	public void Test()
	{
		logger.LogInformation("This is a test message.");
		LogService.Error(logger, "This is an error message.");
	}
}
