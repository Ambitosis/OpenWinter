// Case functions and lengths on Windows-1252 text (this file is Windows-1252 encoded).
#include "parity_inc"

void S(string k, string v) { PLog(k, "[" + v + "]"); }

void main()
{
    string all = " !#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]^_`abcdefghijklmnopqrstuvwxyz{|}~€‚ƒ„…†‡ˆ‰Š‹Œ‘’“”•–—˜™š›œŸ ¡¢£¤¥¦§¨©ª«¬­®¯°±²³´µ¶·¸¹º»¼½¾¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏĞÑÒÓÔÕÖ×ØÙÚÛÜİŞßàáâãäåæçèéêëìíîïğñòóôõö÷øùúûüışÿ";
    string hi = "€‚ƒ„…†‡ˆ‰Š‹Œ‘’“”•–—˜™š›œŸ ¡¢£¤¥¦§¨©ª«¬­®¯°±²³´µ¶·¸¹º»¼½¾¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏĞÑÒÓÔÕÖ×ØÙÚÛÜİŞßàáâãäåæçèéêëìíîïğñòóôõö÷øùúûüışÿ";
    S("case.upper.all", GetStringUpperCase(all));
    S("case.lower.all", GetStringLowerCase(all));
    PLogI("case.len.all", GetStringLength(all));
    PLogI("case.len.hi", GetStringLength(hi));
    S("case.sub.hi", GetSubString(hi, 10, 5));
    PLogI("case.find.hi", FindSubString(hi, "é"));
    S("case.upper.word", GetStringUpperCase("été naïve ß ÿ œ š"));
    S("case.lower.word", GetStringLowerCase("ÉTÉ NAÏVE Œ Š Ÿ"));
    PLogI("case.utf8.len", GetStringLength("Ã©tÃ©"));
    S("case.utf8.upper", GetStringUpperCase("Ã©tÃ©"));
    PLogI("case.stoi.nbsp", StringToInt(" " + "5"));
}
