/////////////////////////////////////////////////////////////////////////////
// <copyright file="TestService.cs" company="Digital Zen Works">
// Copyright © 2026 Digital Zen Works.
// </copyright>
/////////////////////////////////////////////////////////////////////////////

namespace ClientLibrary;

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
		logger.Information("This is a test message.");
		LogService.Error(logger, "This is an error message.");
	}
}
