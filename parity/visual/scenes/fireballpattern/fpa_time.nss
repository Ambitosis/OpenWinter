// Timing helpers for the parity lab. Game-clock seconds run in real time in both engines,
// so the clock measures elapsed time to the millisecond (within one game day).

// Real seconds since midnight, from the game clock.
float PNow()
{
    int nMinPerHour = FloatToInt(HoursToSeconds(1)) / 60;
    return IntToFloat((GetTimeHour() * nMinPerHour + GetTimeMinute()) * 60 + GetTimeSecond()) + IntToFloat(GetTimeMillisecond()) / 1000.0;
}

// Starts the suite's stopwatch.
void PStart()
{
    SetLocalFloat(GetModule(), "PARITY_T0", PNow());
}

// Seconds since PStart.
float PSince()
{
    return PNow() - GetLocalFloat(GetModule(), "PARITY_T0");
}
