param(
    [ValidateSet('Unit','SQLite','SQLServer','PostgreSQL','Oracle','MySQL','MariaDB','Firebird','Db2','Informix','Sybase')]
    [string]$Database = 'Unit'
)
$ErrorActionPreference = 'Stop'
$databases = @('SQLite','SQLServer','PostgreSQL','Oracle','MySQL','MariaDB','Firebird','Db2','Informix','Sybase')
$filter = if ($Database -eq 'Unit') { ($databases | ForEach-Object { "TestCategory!=$_" }) -join '&' } else { "TestCategory=$Database" }
dotnet test Migrator.slnx --no-build --filter $filter --logger "trx;LogFileName=$Database.trx" --results-directory TestResults -- NUnit.NumberOfTestWorkers=0
if ($LASTEXITCODE -ne 0) { throw "Tests failed for $Database" }
[xml]$results = Get-Content "TestResults/$Database.trx"
$counters = $results.TestRun.ResultSummary.Counters
if ([int]$counters.executed -eq 0) { throw "No tests executed for $Database" }
if ([int]$counters.failed -gt 0) { throw "Failures in $Database results" }
Write-Host "$Database : $($counters.passed) passed, $($counters.notExecuted) not executed"
