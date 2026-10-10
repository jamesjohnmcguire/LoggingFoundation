/////////////////////////////////////////////////////////////////////////////
// <copyright file="DailyArchiveFileSinkTests.cs" company="Digital Zen Works">
// Copyright © 2026 Digital Zen Works.
// </copyright>
/////////////////////////////////////////////////////////////////////////////

namespace LoggingService.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;

/// <summary>
/// Exercises daily archiving with a controlled clock and isolated files.
/// </summary>
[TestFixture]
internal sealed class DailyArchiveFileSinkTests
{
	private string temporaryDirectory = string.Empty;
	private string logPath = string.Empty;
	private TestTimeProvider clock = null!;
	private DailyArchiveFileSink? sink;

	/// <summary>
	/// Creates a private directory and a clock just before midnight.
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

		clock = new TestTimeProvider();
	}

	/// <summary>
	/// Releases file handles before removing the test directory.
	/// </summary>
	[TearDown]
	public void TearDown()
	{
		sink?.Dispose();
		sink = null;

		Directory.Delete(temporaryDirectory, recursive: true);
	}

	/// <summary>
	/// Uses the requested filename and makes each message readable immediately.
	/// </summary>
	[Test]
	public void EmitWritesToFixedFilename()
	{
		sink = CreateSink();

		WriteMessage("current-message");

		AssertFileContains("BackUp.log", "current-message");

		string[] files = Directory.GetFiles(temporaryDirectory, "*.log");
		Assert.That(files, Has.Length.EqualTo(1));
	}

	/// <summary>
	/// Moves the previous day's messages before writing the new day's entry.
	/// </summary>
	[Test]
	public void EmitAfterMidnightArchivesPreviousDay()
	{
		sink = CreateSink();
		WriteMessage("yesterday-message");

		clock.AdvanceMinutes(2);
		WriteMessage("today-message");

		AssertFileContains("BackUp20261010.log", "yesterday-message");
		AssertFileContains("BackUp.log", "today-message");

		string active = ReadFile(logPath);
		Assert.That(active, Does.Not.Contain("yesterday-message"));

		string archivePath = Path.Combine(
			temporaryDirectory, "BackUp20261010.log");
		string archive = ReadFile(archivePath);
		Assert.That(archive, Does.Not.Contain("today-message"));
	}

	/// <summary>
	/// Appends on a same-day restart without creating an archive.
	/// </summary>
	[Test]
	public void RestartOnSameDayAppends()
	{
		SeedActiveFile("earlier-message", 0);
		sink = CreateSink();

		WriteMessage("later-message");

		AssertFileContains("BackUp.log", "earlier-message");
		AssertFileContains("BackUp.log", "later-message");

		string[] files = Directory.GetFiles(temporaryDirectory, "*.log");
		Assert.That(files, Has.Length.EqualTo(1));
	}

	/// <summary>
	/// Uses the old file's last-write date after days without a running app.
	/// </summary>
	[Test]
	public void RestartOnLaterDayArchivesLastWriteDate()
	{
		SeedActiveFile("old-message", -3);
		sink = CreateSink();

		WriteMessage("new-message");

		AssertFileContains("BackUp20261007.log", "old-message");
		AssertFileContains("BackUp.log", "new-message");
	}

	/// <summary>
	/// Preserves existing archives, including an existing numbered collision.
	/// </summary>
	[Test]
	public void ArchiveCollisionCreatesNextNumberedFile()
	{
		SeedFile("BackUp20261009.log", "original-archive");
		SeedFile("BackUp20261009_001.log", "first-collision");
		SeedActiveFile("old-active", -1);

		sink = CreateSink();
		WriteMessage("new-active");

		AssertFileContains("BackUp20261009.log", "original-archive");
		AssertFileContains("BackUp20261009_001.log", "first-collision");
		AssertFileContains("BackUp20261009_002.log", "old-active");
		AssertFileContains("BackUp.log", "new-active");
	}

	/// <summary>
	/// Keeps only the requested number of archives for this exact log name.
	/// </summary>
	[Test]
	public void RetentionPreservesActiveFileAndUnrelatedFiles()
	{
		SeedFile("BackUp20261007.log", "oldest");
		SeedFile("BackUp20261008.log", "retained");
		SeedFile("Other20261001.log", "unrelated");
		SeedFile("BackUp20261001-notes.log", "notes");
		SeedActiveFile("previous-day", -1);

		sink = CreateSink(2);
		WriteMessage("current-day");

		string oldest = Path.Combine(temporaryDirectory, "BackUp20261007.log");
		bool oldestExists = File.Exists(oldest);
		Assert.That(oldestExists, Is.False);

		AssertFileContains("BackUp20261008.log", "retained");
		AssertFileContains("BackUp20261009.log", "previous-day");
		AssertFileContains("BackUp.log", "current-day");
		AssertFileContains("Other20261001.log", "unrelated");
		AssertFileContains("BackUp20261001-notes.log", "notes");
	}

	/// <summary>
	/// Allows callers to retain all archives.
	/// </summary>
	[Test]
	public void NullRetentionPreservesAllArchives()
	{
		SeedFile("BackUp20200101.log", "old-archive");
		SeedActiveFile("previous-day", -1);

		sink = CreateSink(null);
		WriteMessage("current-day");

		AssertFileContains("BackUp20200101.log", "old-archive");
		AssertFileContains("BackUp20261009.log", "previous-day");
	}

	/// <summary>
	/// Retains 31 archived files by default, in addition to the active file.
	/// </summary>
	[Test]
	public void DefaultRetentionKeeps31Archives()
	{
		DateTime date = new(2026, 8, 1);

		for (int index = 0; index < 32; index++)
		{
			DateTime archiveDate = date.AddDays(index);
			string name = archiveDate.ToString(
				"yyyyMMdd",
				CultureInfo.InvariantCulture);
			string fileName = "BackUp" + name + ".log";
			SeedFile(fileName, "historical");
		}

		SeedActiveFile("previous-day", -1);
		sink = CreateSink();
		WriteMessage("current-day");

		string[] files = Directory.GetFiles(temporaryDirectory, "*.log");
		Assert.That(files, Has.Length.EqualTo(32));
		AssertFileContains("BackUp.log", "current-day");
	}

	/// <summary>
	/// Does not create an empty archive for an old empty active file.
	/// </summary>
	[Test]
	public void EmptyOldFileDoesNotCreateArchive()
	{
		SeedActiveFile(string.Empty, -1);
		sink = CreateSink();

		WriteMessage("first-message");

		string[] files = Directory.GetFiles(temporaryDirectory, "*.log");
		Assert.That(files, Has.Length.EqualTo(1));
		AssertFileContains("BackUp.log", "first-message");
	}

	/// <summary>
	/// A backward clock change does not roll into an earlier date.
	/// </summary>
	[Test]
	public void ClockMovingBackwardDoesNotArchive()
	{
		sink = CreateSink();
		WriteMessage("before-clock-change");

		clock.AdvanceDays(-1);
		WriteMessage("after-clock-change");

		string[] files = Directory.GetFiles(temporaryDirectory, "*.log");
		Assert.That(files, Has.Length.EqualTo(1));
		AssertFileContains("BackUp.log", "before-clock-change");
		AssertFileContains("BackUp.log", "after-clock-change");
	}

	/// <summary>
	/// Serializes concurrent writes and performs one rollover at midnight.
	/// </summary>
	[Test]
	public void ConcurrentWritesAfterMidnightPreserveEveryMessage()
	{
		sink = CreateSink();
		WriteMessage("previous-day");

		clock.AdvanceDays(1);
		Parallel.For(0, 100, WriteConcurrentMessage);

		sink.Dispose();

		string[] lines = File.ReadAllLines(logPath);
		HashSet<string> messages = new(lines);

		Assert.That(lines, Has.Length.EqualTo(100));
		Assert.That(messages, Has.Count.EqualTo(100));
		AssertFileContains("BackUp20261010.log", "previous-day");

		for (int index = 0; index < 100; index++)
		{
			string expected = "message-" + index;
			Assert.That(messages, Does.Contain(expected));
		}
	}

	/// <summary>
	/// Prevents a second writer from renaming or writing this sink's files.
	/// </summary>
	[Test]
	public void SecondWriterFailsUntilFirstIsDisposed()
	{
		sink = CreateSink();
		WriteMessage("first-writer");

		Assert.Throws<IOException>(OpenSecondSink);

		sink.Dispose();
		sink = CreateSink();
		WriteMessage("replacement-writer");

		AssertFileContains("BackUp.log", "first-writer");
		AssertFileContains("BackUp.log", "replacement-writer");
	}

	/// <summary>
	/// Cleanup is repeatable and a disposed sink rejects writes.
	/// </summary>
	[Test]
	public void DisposeReleasesHandlesAndRejectsFurtherWrites()
	{
		sink = CreateSink();
		sink.Dispose();
		sink.Dispose();

		Assert.Throws<ObjectDisposedException>(WriteAfterDisposal);

		using FileStream stream = File.Open(
			logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
		Assert.That(stream.CanWrite, Is.True);
	}

	/// <summary>
	/// Uses the configured local date rather than the UTC date on restart.
	/// </summary>
	[Test]
	public void RestartUsesLocalCalendarDate()
	{
		TimeSpan offset = TimeSpan.FromHours(2);
		clock.Zone = TimeZoneInfo.CreateCustomTimeZone(
			"TestOffset", offset, "TestOffset", "TestOffset");
		SeedActiveFile("local-previous-day", -1);

		sink = CreateSink();
		WriteMessage("local-current-day");

		AssertFileContains("BackUp20261010.log", "local-previous-day");
		AssertFileContains("BackUp.log", "local-current-day");
	}

	/// <summary>
	/// A failed rename preserves the incoming message and retries later.
	/// </summary>
	[Test]
	public void FailedRotationContinuesWritingAndRetries()
	{
		sink = CreateSink();
		WriteMessage("previous-day");

		string blockedArchive = Path.Combine(
			temporaryDirectory, "BackUp20261010.log");
		Directory.CreateDirectory(blockedArchive);
		clock.AdvanceDays(1);

		WriteMessage("message-during-failure");

		AssertFileContains("BackUp.log", "previous-day");
		AssertFileContains("BackUp.log", "message-during-failure");

		Directory.Delete(blockedArchive);
		WriteMessage("message-after-recovery");

		AssertFileContains("BackUp20261010.log", "previous-day");
		AssertFileContains("BackUp20261010.log", "message-during-failure");
		AssertFileContains("BackUp.log", "message-after-recovery");
	}

	/// <summary>
	/// Orders numbered collisions numerically when applying retention.
	/// </summary>
	[Test]
	public void RetentionOrdersCollisionNumbersNumerically()
	{
		SeedFile("BackUp20261009_999.log", "older-collision");
		SeedFile("BackUp20261009_1000.log", "newer-collision");

		sink = CreateSink(1);
		WriteMessage("current-day");

		string olderPath = Path.Combine(
			temporaryDirectory, "BackUp20261009_999.log");
		bool olderExists = File.Exists(olderPath);

		Assert.That(olderExists, Is.False);
		AssertFileContains("BackUp20261009_1000.log", "newer-collision");
	}

	private static string ReadFile(string path)
	{
		using FileStream stream = new(
			path,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete);
		using StreamReader reader = new(stream);

		return reader.ReadToEnd();
	}

	private DailyArchiveFileSink CreateSink(int? retainedArchiveCount = 31)
	{
		MessageTemplateTextFormatter formatter = new(
			"{Message:lj}{NewLine}", CultureInfo.InvariantCulture);

		return new DailyArchiveFileSink(
			logPath, formatter, clock, retainedArchiveCount);
	}

	private void OpenSecondSink()
	{
		using DailyArchiveFileSink secondSink = CreateSink();
	}

	private void WriteAfterDisposal()
	{
		WriteMessage("disposed-message");
	}

	private void WriteConcurrentMessage(int index)
	{
		string message = "message-" + index;
		WriteMessage(message);
	}

	private void WriteMessage(string message)
	{
		MessageTemplateParser parser = new();
		MessageTemplate template = parser.Parse(message);
		DateTimeOffset timestamp = clock.GetUtcNow();
		LogEvent logEvent = new(
			timestamp, LogEventLevel.Information, null, template, []);

		sink!.Emit(logEvent);
	}

	private void SeedActiveFile(string contents, int dayOffset)
	{
		File.WriteAllText(logPath, contents);

		DateTimeOffset now = clock.GetUtcNow();
		DateTimeOffset lastWrite = now.AddDays(dayOffset);
		File.SetLastWriteTimeUtc(logPath, lastWrite.UtcDateTime);
	}

	private void SeedFile(string name, string contents)
	{
		string path = Path.Combine(temporaryDirectory, name);
		File.WriteAllText(path, contents);
	}

	private void AssertFileContains(string name, string expected)
	{
		string path = Path.Combine(temporaryDirectory, name);
		bool exists = File.Exists(path);
		Assert.That(exists, Is.True, name);

		string contents = ReadFile(path);
		Assert.That(contents, Does.Contain(expected), name);
	}

	private sealed class TestTimeProvider : TimeProvider
	{
		private DateTimeOffset currentTime =
			new(2026, 10, 10, 23, 59, 0, TimeSpan.Zero);

		public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Utc;

		public override TimeZoneInfo LocalTimeZone => Zone;

		public override DateTimeOffset GetUtcNow()
		{
			return currentTime;
		}

		public void AdvanceMinutes(int minutes)
		{
			currentTime = currentTime.AddMinutes(minutes);
		}

		public void AdvanceDays(int days)
		{
			currentTime = currentTime.AddDays(days);
		}
	}
}
