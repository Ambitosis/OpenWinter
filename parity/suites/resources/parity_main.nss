// Regular expressions, 2DA lookups, the talk table and ResMan queries (base game data only).
#include "parity_inc"

void J(string k, json j) { PLog(k, JsonDump(j) + " err=[" + JsonGetError(j) + "]"); }

void Regex()
{
    J("match.none", RegExpMatch("nothing", "test value"));
    J("match.bol", RegExpMatch("^test", "test value"));
    J("match.groups", RegExpMatch("^(test) (.+)$", "test value"));
    J("match.optional", RegExpMatch("(a)?(b)", "b"));
    J("match.nested", RegExpMatch("((a)(b(c)))", "xabcx"));
    J("match.bad", RegExpMatch("[", "test value"));
    J("match.badparen", RegExpMatch("(a", "a"));
    J("match.badrepeat", RegExpMatch("*a", "a"));
    J("match.icase", RegExpMatch("TEST", "a test", REGEXP_ECMASCRIPT | REGEXP_ICASE));
    J("match.nosubs", RegExpMatch("(t)(e)", "test", REGEXP_ECMASCRIPT | REGEXP_NOSUBS));
    J("match.basic", RegExpMatch("\\(a\\)b*", "abbb", REGEXP_BASIC));
    J("match.extended", RegExpMatch("(a|b)+", "abba", REGEXP_EXTENDED));
    J("match.notbol", RegExpMatch("^a", "abc", REGEXP_ECMASCRIPT, REGEXP_MATCH_NOT_BOL));
    J("match.lookahead", RegExpMatch("a(?=b)", "ab"));
    J("match.lookbehind", RegExpMatch("(?<=a)b", "ab"));
    J("match.named", RegExpMatch("(?<x>a)", "a"));
    J("match.classes", RegExpMatch("\\d+\\s\\w+", "  12 ab"));
    J("match.posix", RegExpMatch("[[:digit:]]+", "x42", REGEXP_EXTENDED));
    J("match.posix.ecma", RegExpMatch("[[:alpha:]]+", "42xy"));
    J("match.backref", RegExpMatch("(a)\\1", "aa"));
    J("match.lazy", RegExpMatch("a+?", "aaa"));
    J("match.dot.newline", RegExpMatch("a.b", "a\nb"));
    J("match.empty", RegExpMatch("", "abc"));
    J("match.unicode", RegExpMatch("\\w+", "\xe9t\xe9"));
    J("iter.doc", RegExpIterate("(\\d)(\\S+)", "1i 2am 3 4asentence"));
    J("iter.none", RegExpIterate("x", "abc"));
    J("iter.empty", RegExpIterate("a*", "baa"));
    J("iter.bad", RegExpIterate("(", "abc"));
    J("iter.first", RegExpIterate("a", "aaa", REGEXP_ECMASCRIPT, REGEXP_FORMAT_FIRST_ONLY));
    PLog("rep.doc", RegExpReplace("a+", "vaaalue", "[$&]"));
    PLog("rep.groups", RegExpReplace("(\\w+) (\\w+)", "hello world", "$2 $1"));
    PLog("rep.dollar", RegExpReplace("a", "aba", "$$"));
    PLog("rep.prepost", RegExpReplace("b", "abc", "[$`|$']"));
    PLog("rep.twodigit", RegExpReplace("(a)", "a", "$01$10x"));
    PLog("rep.missinggroup", RegExpReplace("(a)", "a", "$2"));
    PLog("rep.sed", RegExpReplace("(a)", "xax", "<\\1&>", REGEXP_ECMASCRIPT, REGEXP_FORMAT_SED));
    PLog("rep.nocopy", RegExpReplace("a", "xaya", "!", REGEXP_ECMASCRIPT, REGEXP_FORMAT_NO_COPY));
    PLog("rep.firstonly", RegExpReplace("a", "aaa", "b", REGEXP_ECMASCRIPT, REGEXP_FORMAT_FIRST_ONLY));
    PLog("rep.bad", "[" + RegExpReplace("[", "abc", "x") + "]");
    PLog("rep.empty", RegExpReplace("x*", "abc", "-"));
    PLog("rep.icase", RegExpReplace("A", "aAa", "b", REGEXP_ECMASCRIPT | REGEXP_ICASE));
}

void TwoDA()
{
    PLog("2da.ok", Get2DAString("appearance", "LABEL", 0));
    PLog("2da.case.col", Get2DAString("appearance", "label", 0));
    PLog("2da.case.file", Get2DAString("APPEARANCE", "LABEL", 0));
    PLog("2da.stars", "[" + Get2DAString("appearance", "PORTRAIT", 2) + "]");
    PLog("2da.badrow", "[" + Get2DAString("appearance", "LABEL", 100000) + "]");
    PLog("2da.negrow", "[" + Get2DAString("appearance", "LABEL", -1) + "]");
    PLog("2da.badcol", "[" + Get2DAString("appearance", "NOSUCHCOLUMN", 0) + "]");
    PLog("2da.nofile", "[" + Get2DAString("nosuch2da", "LABEL", 0) + "]");
    PLog("2da.emptycol", "[" + Get2DAString("appearance", "", 0) + "]");
    PLog("2da.rowlabelcol", "[" + Get2DAString("appearance", "Row", 0) + "]");
    PLog("2da.quoted", "[" + Get2DAString("classes", "Label", 0) + "]");
    PLogI("2da.rows", Get2DARowCount("appearance") > 800);
    PLogI("2da.rows.racialtypes", Get2DARowCount("racialtypes"));
    PLogI("2da.rows.nofile", Get2DARowCount("nosuch2da"));
    int i;
    for (i = -1; i <= 3; i++) PLog("2da.col." + IntToString(i), "[" + Get2DAColumn("racialtypes", i) + "]");
    PLog("2da.col.last", "[" + Get2DAColumn("racialtypes", 1000) + "]");
    PLog("2da.col.nofile", "[" + Get2DAColumn("nosuch2da", 0) + "]");
    PLog("2da.int", Get2DAString("racialtypes", "AbilitiesPointBuyNumber", 6));
}

void Tlk()
{
    PLog("tlk.0", GetStringByStrRef(0));
    PLog("tlk.1", GetStringByStrRef(1));
    PLog("tlk.neg", "[" + GetStringByStrRef(-1) + "]");
    PLog("tlk.huge", "[" + GetStringByStrRef(1000000) + "]");
    PLog("tlk.custom", "[" + GetStringByStrRef(16777216) + "]");
    PLog("tlk.custom1", "[" + GetStringByStrRef(16777217) + "]");
    PLog("tlk.female", GetStringByStrRef(6, GENDER_FEMALE));
    PLog("tlk.male", GetStringByStrRef(6, GENDER_MALE));
    PLog("tlk.gender.bad", GetStringByStrRef(6, 7));
    SetTlkOverride(1, "override!");
    PLog("tlk.override", GetStringByStrRef(1));
    SetTlkOverride(1);
    PLog("tlk.override.cleared", GetStringByStrRef(1));
    SetTlkOverride(16777300, "custom override");
    PLog("tlk.override.custom", GetStringByStrRef(16777300));
}

void ResMan()
{
    PLog("alias.module.ncs", ResManGetAliasFor("parity_main", RESTYPE_NCS));
    PLog("alias.module.nss", ResManGetAliasFor("parity_main", RESTYPE_NSS));
    PLog("alias.area", ResManGetAliasFor("parity", RESTYPE_ARE));
    PLog("alias.ifo", ResManGetAliasFor("module", RESTYPE_IFO));
    PLog("alias.base.utw", ResManGetAliasFor("nw_waypoint001", RESTYPE_UTW));
    PLog("alias.base.2da", ResManGetAliasFor("appearance", RESTYPE_2DA));
    PLog("alias.base.nss", ResManGetAliasFor("nwscript", RESTYPE_NSS));
    PLog("alias.missing", "[" + ResManGetAliasFor("nosuchres", RESTYPE_UTC) + "]");
    PLog("alias.case", ResManGetAliasFor("PARITY_MAIN", RESTYPE_NCS));
    int i;
    for (i = 0; i <= 3; i++)
    {
        PLog("prefix.module." + IntToString(i), "[" + ResManFindPrefix("parity_", RESTYPE_NCS, i) + "]");
        PLog("prefix.base." + IntToString(i), "[" + ResManFindPrefix("nw_waypoint", RESTYPE_UTW, i, TRUE) + "]");
        PLog("prefix.nobase." + IntToString(i), "[" + ResManFindPrefix("nw_waypoint", RESTYPE_UTW, i) + "]");
    }
    PLog("prefix.empty", "[" + ResManFindPrefix("", RESTYPE_ARE, 1) + "]");
    PLog("prefix.keytable", "[" + ResManFindPrefix("parity", RESTYPE_ARE, 1, FALSE, "MODULE:") + "]");
    PLog("contents.nss", ResManGetFileContents("parity_inc", RESTYPE_NSS) == "" ? "empty" : "found");
    PLog("contents.txt.hex", GetStringLeft(ResManGetFileContents("parity", RESTYPE_ARE, RESMAN_FILE_CONTENTS_FORMAT_HEX), 16));
    PLog("contents.b64", GetStringLeft(ResManGetFileContents("parity", RESTYPE_ARE, RESMAN_FILE_CONTENTS_FORMAT_BASE64), 12));
    PLog("contents.raw", GetStringLeft(ResManGetFileContents("parity", RESTYPE_ARE), 8));
    PLog("contents.missing", "[" + ResManGetFileContents("nosuchres", RESTYPE_ARE) + "]");
}

void Misc()
{
    PLogI("hash.empty", HashString(""));
    PLogI("hash.abc", HashString("abc"));
    PLogI("hash.long", HashString("The quick brown fox jumps over the lazy dog"));
    string u = GetRandomUUID();
    PLogI("uuid.len", GetStringLength(u));
    PLog("uuid.dashes", GetSubString(u, 8, 1) + GetSubString(u, 13, 1) + GetSubString(u, 18, 1) + GetSubString(u, 23, 1));
    PLog("uuid.version", GetSubString(u, 14, 1));
    PLogI("pattern.1", TestStringAgainstPattern("**hello**", "say hello now"));
    PLogI("pattern.2", TestStringAgainstPattern("*n", "123"));
    PLogI("pattern.3", TestStringAgainstPattern("*a", "abc"));
    PLogI("pattern.4", TestStringAgainstPattern("abc", "ABC"));
    PLogI("pattern.5", TestStringAgainstPattern("**", ""));
    PLogI("pattern.6", TestStringAgainstPattern("*w", "hello world"));
    PLogI("pattern.7", TestStringAgainstPattern("**(hello|bye)**", "well bye then"));
    PLogI("pattern.8", TestStringAgainstPattern("**HeLLo**", "say hello now"));
    int i;
    for (i = 0; i < 4; i++) PLog("pattern.7.sub" + IntToString(i), "[" + GetMatchedSubstring(i) + "]");
    PLogI("pattern.7.count", GetMatchedSubstringsCount());
    PLogI("pattern.9", TestStringAgainstPattern("*n *a", "42 abc"));
    for (i = 0; i < 4; i++) PLog("pattern.9.sub" + IntToString(i), "[" + GetMatchedSubstring(i) + "]");
    PLogI("pattern.9.count", GetMatchedSubstringsCount());
    PLogI("pattern.10", TestStringAgainstPattern("a*b", "a*b"));
    PLogI("pattern.11", TestStringAgainstPattern("hi|bye", "bye"));
    PLogI("pattern.12", TestStringAgainstPattern("*p", "!?"));
    PLogI("pattern.13", TestStringAgainstPattern("", ""));
}

void main()
{
    DelayCommand(1.0, Regex());
    DelayCommand(1.05, ExecuteScript("res_regex"));
    DelayCommand(1.1, TwoDA());
    DelayCommand(1.2, Tlk());
    DelayCommand(1.3, ResMan());
    DelayCommand(1.4, Misc());
    DelayCommand(2.0, PDone());
}
