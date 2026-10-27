// Module post-process pass: the scene's own fsfbpostpr (a colour grade, a depth effect and a readout of the
// engine's values), switched by SetShaderUniformInt bank 1. The readout is taken under the area's own wind,
// after SetAreaWind, in rain and snow and at another hour.
#include "visual_inc"

void VNote(string sWhat)
{
    WriteTimestampedLogEntry("VISUAL|note|" + sWhat + "|" + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute()) + ":"
        + IntToString(GetTimeSecond()) + "." + IntToString(GetTimeMillisecond()) + "|us " + IntToString(GetMicrosecondCounter())
        + "|weather " + IntToString(GetWeather(GetArea(GetFirstPC()))));
}

void Shot(string sName)
{
    VShot(sName);
    VNote(sName);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    object oArea = GetArea(oPC);
    VBegin(oPC);
    VHidePC(oPC);
    SetAreaDefaultGrassDisabled(oArea, TRUE);
    VPlaceable(oPC, "plc_statue1", 3.0, 2.0);
    VPlaceable(oPC, "plc_brazier", -3.0, 4.0);
    VPlaceable(oPC, "plc_chest1", 1.0, -2.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 12.0, 55.0));
    DelayCommand(5.0, Shot("a_view"));
    DelayCommand(6.0, SetShaderUniformFloat(oPC, SHADER_UNIFORM_1, 1.0));
    DelayCommand(6.0, SetShaderUniformInt(oPC, SHADER_UNIFORM_1, 1));
    DelayCommand(8.0, Shot("b_grade"));
    DelayCommand(9.0, SetShaderUniformInt(oPC, SHADER_UNIFORM_1, 2));
    DelayCommand(11.0, Shot("c_depth"));
    DelayCommand(12.0, VCamera(oPC, 0.0, 20.0, 75.0));
    DelayCommand(14.0, Shot("d_depthlow"));
    DelayCommand(15.0, VCamera(oPC, 90.0, 12.0, 55.0));
    DelayCommand(15.0, SetShaderUniformInt(oPC, SHADER_UNIFORM_1, 3));
    DelayCommand(17.0, Shot("e_readout"));
    DelayCommand(19.0, Shot("f_readout2"));
    DelayCommand(20.0, SetAreaWind(oArea, Vector(1.0, 0.0, 0.0), 2.0, 0.0, 0.0));
    DelayCommand(22.0, Shot("g_wind"));
    DelayCommand(23.0, SetAreaWind(oArea, Vector(0.0, 3.0, 0.0), 0.5, 45.0, 10.0));
    DelayCommand(25.0, Shot("h_wind2"));
    DelayCommand(26.0, SetWeather(oArea, WEATHER_RAIN));
    DelayCommand(27.0, Shot("i_rain"));
    DelayCommand(33.0, Shot("j_rain_later"));
    DelayCommand(34.0, SetWeather(oArea, WEATHER_SNOW));
    DelayCommand(37.0, Shot("k_snow"));
    DelayCommand(38.0, SetWeather(oArea, WEATHER_CLEAR));
    DelayCommand(38.0, SetTime(18, 30, 0, 0));
    DelayCommand(41.0, Shot("l_time"));
    DelayCommand(44.0, VDone());
}
