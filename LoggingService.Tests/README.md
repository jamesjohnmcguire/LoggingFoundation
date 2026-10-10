# LoggingService Tests

Run from the LoggingFoundation directory:

```sh
dotnet test LoggingService.Tests/LoggingService.Tests.csproj
```

The project is included in both LoggingFoundation.slnx and the parent
BackUpManager.slnx. The parent DevelopmentTools/Build.cmd and its CI test
command therefore discover these tests without additional script changes.

These are characterization tests for the existing Configure API used by
BackupManager: dated file and console output, minimum severity, reopening,
missing directories, message helpers, and exception details. They do not
introduce the proposed fixed active filename or test future rollover rules.

Each test uses a unique temporary directory, closes its logger, restores
Console.Out and Log.Logger, and deletes its own files. The fixture is
nonparallel because these two globals are process-wide. No application
configuration, credentials, network services, or real backup logs are used.
Date assertions allow a midnight boundary without sleeps.

The legacy Initialize/CreateLogger path is outside this first set: it owns
a static logger factory and file sink without a public disposal/reset API.
Testing it cleanly needs process isolation or a separately reviewed lifecycle
change. The caller-context Exception overload also needs separate attention:
it currently emits literal {caller} and {lineNumber} placeholders. Tests here
cover the two-argument exception helper without codifying that defect.
