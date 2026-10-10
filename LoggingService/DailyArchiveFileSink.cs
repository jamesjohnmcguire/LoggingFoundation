/////////////////////////////////////////////////////////////////////////////
// <copyright file="DailyArchiveFileSink.cs" company="Digital Zen Works">
// Copyright © 2026 Digital Zen Works.
// </copyright>
/////////////////////////////////////////////////////////////////////////////

namespace LoggingService;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

/// <summary>
/// Keeps an active filename and archives its contents on local day changes.
/// </summary>
internal sealed class DailyArchiveFileSink : ILogEventSink, IDisposable
{
	private readonly object syncRoot = new();
	private readonly string path;
	private readonly string directory;
	private readonly string name;
	private readonly string extension;
	private readonly ITextFormatter formatter;
	private readonly TimeProvider timeProvider;
	private readonly int? retainedArchiveCount;
	private readonly FileStream ownershipLock;

	private StreamWriter writer;
	private DateTime activeDate;
	private bool retentionPending = true;
	private bool disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DailyArchiveFileSink"/>
	/// class and acquires exclusive ownership of the log path.
	/// </summary>
	/// <param name="path">The active file path.</param>
	/// <param name="formatter">The event formatter.</param>
	/// <param name="timeProvider">The clock and local time zone.</param>
	/// <param name="retainedArchiveCount">The archive limit, or null.</param>
	public DailyArchiveFileSink(
		string path,
		ITextFormatter formatter,
		TimeProvider timeProvider,
		int? retainedArchiveCount = 31)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(formatter);
		ArgumentNullException.ThrowIfNull(timeProvider);

		if (retainedArchiveCount < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(retainedArchiveCount),
				"Keep at least one archive, or use null to keep all archives.");
		}

		this.path = Path.GetFullPath(path);
		directory = Path.GetDirectoryName(this.path)!;
		name = Path.GetFileNameWithoutExtension(this.path);
		extension = Path.GetExtension(this.path);
		this.formatter = formatter;
		this.timeProvider = timeProvider;
		this.retainedArchiveCount = retainedArchiveCount;

		Directory.CreateDirectory(directory);

		// Keep this sidecar in place: deleting it can let two processes lock
		// different file handles for the same path during a restart.
		string lockPath = this.path + ".lock";
		ownershipLock = new FileStream(
			lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

		try
		{
			activeDate = GetCurrentDate();

			FileInfo existingFile = new(this.path);

			if (existingFile.Exists && existingFile.Length > 0)
			{
				DateTime lastWriteUtc = existingFile.LastWriteTimeUtc;
				TimeZoneInfo zone = timeProvider.LocalTimeZone;
				DateTime localLastWrite =
					TimeZoneInfo.ConvertTimeFromUtc(lastWriteUtc, zone);

				activeDate = localLastWrite.Date;
			}

			writer = OpenWriter();
		}
		catch
		{
			ownershipLock.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Writes and flushes an event, rotating before the first new-day entry.
	/// </summary>
	/// <param name="logEvent">The event to write.</param>
	public void Emit(LogEvent logEvent)
	{
		ArgumentNullException.ThrowIfNull(logEvent);

		lock (syncRoot)
		{
			ObjectDisposedException.ThrowIf(disposed, this);

			DateTime currentDate = GetCurrentDate();

			if (currentDate > activeDate)
			{
				Rotate(currentDate);
			}

			formatter.Format(logEvent, writer);
			writer.Flush();

			if (retentionPending)
			{
				ApplyRetention();
				retentionPending = false;
			}
		}
	}

	/// <summary>
	/// Flushes output and releases ownership of the active file.
	/// </summary>
	public void Dispose()
	{
		lock (syncRoot)
		{
			if (disposed)
			{
				return;
			}

			disposed = true;

			try
			{
				writer.Dispose();
			}
			finally
			{
				ownershipLock.Dispose();
			}
		}
	}

	private void ApplyRetention()
	{
		if (retainedArchiveCount == null)
		{
			return;
		}

		try
		{
			SortedDictionary<(DateTime Date, int Sequence), string> archives =
				FindArchives();
			int removeCount = archives.Count - retainedArchiveCount.Value;

			foreach (string archive in archives.Values)
			{
				if (removeCount <= 0)
				{
					break;
				}

				File.Delete(archive);
				removeCount--;
			}
		}
		catch (IOException exception)
		{
			SelfLog.WriteLine("Log archive cleanup failed: {0}", exception);
		}
		catch (UnauthorizedAccessException exception)
		{
			SelfLog.WriteLine("Log archive cleanup failed: {0}", exception);
		}
	}

	private SortedDictionary<(DateTime Date, int Sequence), string>
		FindArchives()
	{
		SortedDictionary<(DateTime Date, int Sequence), string> archives =
			new();
		string[] files = Directory.GetFiles(directory);

		foreach (string file in files)
		{
			string fileExtension = Path.GetExtension(file);

			if (!string.Equals(
				fileExtension, extension, StringComparison.Ordinal))
			{
				continue;
			}

			string fileName = Path.GetFileNameWithoutExtension(file);
			bool matchesName = fileName.StartsWith(
				name, StringComparison.Ordinal);

			if (!matchesName || fileName.Length < name.Length + 8)
			{
				continue;
			}

			string dateText = fileName.Substring(name.Length, 8);
			bool validDate = DateTime.TryParseExact(
				dateText,
				"yyyyMMdd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out DateTime date);

			if (!validDate)
			{
				continue;
			}

			int sequence = 0;
			string suffix = fileName.Substring(name.Length + 8);

			if (suffix.Length > 0)
			{
				if (!suffix.StartsWith('_'))
				{
					continue;
				}

				string sequenceText = suffix.Substring(1);
				bool validSequence = int.TryParse(
					sequenceText,
					NumberStyles.None,
					CultureInfo.InvariantCulture,
					out sequence);

				if (!validSequence || sequence < 1)
				{
					continue;
				}

				string expectedSequence = sequence.ToString(
					"D3", CultureInfo.InvariantCulture);

				if (sequenceText != expectedSequence)
				{
					continue;
				}
			}

			archives.Add((date, sequence), file);
		}

		return archives;
	}

	private DateTime GetCurrentDate()
	{
		DateTimeOffset now = timeProvider.GetLocalNow();

		return now.Date;
	}

	private string GetArchivePath(int sequence)
	{
		string date = activeDate.ToString(
			"yyyyMMdd", CultureInfo.InvariantCulture);
		string archiveName = name + date;

		if (sequence > 0)
		{
			string number = sequence.ToString(
				"D3", CultureInfo.InvariantCulture);

			archiveName += "_" + number;
		}

		archiveName += extension;

		return Path.Combine(directory, archiveName);
	}

	private StreamWriter OpenWriter()
	{
		FileStream stream = new(
			path, FileMode.Append, FileAccess.Write, FileShare.Read);

		try
		{
			UTF8Encoding encoding = new(false);

			return new StreamWriter(stream, encoding);
		}
		catch
		{
			stream.Dispose();
			throw;
		}
	}

	private void Rotate(DateTime currentDate)
	{
		long length = writer.BaseStream.Length;

		if (length == 0)
		{
			activeDate = currentDate;
			return;
		}

		writer.Dispose();

		try
		{
			int sequence = 0;
			string archivePath = GetArchivePath(sequence);

			while (File.Exists(archivePath))
			{
				sequence++;
				archivePath = GetArchivePath(sequence);
			}

			// Never replace an existing archive. If another program creates
			// this name concurrently, keep writing the old active file.
			File.Move(path, archivePath);

			activeDate = currentDate;
			retentionPending = true;
		}
		catch (IOException exception)
		{
			SelfLog.WriteLine("Log rotation failed; will retry: {0}", exception);
		}
		catch (UnauthorizedAccessException exception)
		{
			SelfLog.WriteLine("Log rotation failed; will retry: {0}", exception);
		}
		finally
		{
			writer = OpenWriter();
		}
	}
}
