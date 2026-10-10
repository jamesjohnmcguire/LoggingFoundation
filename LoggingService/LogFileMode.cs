/////////////////////////////////////////////////////////////////////////////
// <copyright file="LogFileMode.cs" company="Digital Zen Works">
// Copyright © 2026 Digital Zen Works.
// </copyright>
/////////////////////////////////////////////////////////////////////////////

namespace LoggingService;

/// <summary>
/// Selects how active and archived log files are named.
/// </summary>
public enum LogFileMode
{
	/// <summary>
	/// Uses Serilog's date-suffixed active files.
	/// </summary>
	DatedFiles,

	/// <summary>
	/// Keeps the requested active filename and dates completed days.
	/// </summary>
	FixedNameWithDailyArchives
}
