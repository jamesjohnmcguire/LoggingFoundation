# File logging

Call `LogService.Configure(path)` to keep the existing Serilog behavior:
the active filename includes the date, and the newest 31 files are retained.

To keep the active filename fixed:

```csharp
LogService.Configure(
    logFilePath,
    LogFileMode.FixedNameWithDailyArchives,
    retainedFileCountLimit: 31);
```

For `BackUp.log`, this mode writes current messages to `BackUp.log`.
On the first event after a local calendar day changes, it closes that file,
moves it to `BackUpyyyyMMdd.log`, and opens a new `BackUp.log`.
There is no background timer. On restart, the old file's last-write time
determines its archive date. An empty file is reused without archiving.

Existing archives are never overwritten. Name collisions produce
`BackUpyyyyMMdd_001.log`, `_002.log`, and so on. Retention keeps 31 archives
by default, plus the active file; pass null to retain all archives.
Cleanup recognizes only matching dated and numbered filenames. Existing
date-suffixed logs from the previous mode count toward retention. They are
not merged into the active file. Historical content already combined in an
undated file is archived together using that file's last-write date.

The fixed mode flushes each event and serializes writes from multiple
threads. Only one process can own a given path. An empty `BackUp.log.lock`
sidecar reserves ownership across renames; its presence alone does not mean
the app is running. Keep the sidecar in place. A second owner fails during
configuration rather than interfering with the active writer.

If a rename temporarily fails, logging continues in the active file and
rotation is retried on the next event. Details go to Serilog's SelfLog.
Retention cleanup is best effort. Fixed mode rotates by day, not file size;
a busy day can produce a large file.

Configure once at startup. Call `Log.CloseAndFlush()` in a shutdown
`finally` block to release file handles (or await `Log.CloseAndFlushAsync()`
in async applications). Before reconfiguring, stop logging
producers and close the previous logger. Serilog owns the configured sink.
The legacy `Initialize/CreateLogger` API retains its existing dated behavior.

The naming mode and retention limit are library parameters, ready for a
future configuration or command-line setting. No user-facing option is
introduced here.
