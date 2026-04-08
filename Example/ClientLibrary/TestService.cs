/////////////////////////////////////////////////////////////////////////////
// <copyright file="TestService.cs" company="Digital Zen Works">
// Copyright © 2026 Digital Zen Works.
// </copyright>
/////////////////////////////////////////////////////////////////////////////

namespace ClientLibrary;

using LoggingService;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides functionality for testing logging operations using the specified
/// logger.
/// </summary>
public class TestService
{
	private readonly ILogger logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="TestService"/> class using
	/// the specified logger.
	/// </summary>
	/// <param name="logger">The logger to use for recording diagnostic and
	/// operational messages. Cannot be null.</param>
	public TestService(ILogger logger)
	{
		this.logger = logger;
	}

	/// <summary>
	/// Writes a test informational message and an error message to the
	/// configured logger.
	/// </summary>
	public void Test()
	{
		logger.Information("This is a test message.");
		LogService.Error(logger, "This is an error message.");
	}
}
