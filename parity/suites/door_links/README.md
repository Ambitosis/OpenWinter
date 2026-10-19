# Linked door state

`A` has a one-way authored link to locked `B`. Scripts open A, close B, close A,
reopen A, and close A again. Every state and OnOpen/OnClosed callback is logged.

Native opens/closes the destination before the source callback without clearing
its lock or invoking its hooks. Directly operating B does not change A.
The corrected client/server implementation matches all 33 readings.

Run `dotnet run --project src/OpenWinter.Tools -c Release -- parity-run door_links`.
See [door evidence](../../../docs/DOORS.md) for captures and limits.
