// More std::regex behaviour: grammars other than ECMAScript act as POSIX basic, escapes, errors and replace formats.
#include "parity_inc"
void J(string k, json j) { PLog(k, JsonDump(j) + " err=[" + JsonGetError(j) + "]"); }
void T(string k, string re, string s, int syn = 0, int mf = 0) { J(k, RegExpMatch(re, s, syn, mf)); }
void Go()
{
    T("ere.alt", "a|b", "xbx", 2);
    T("ere.plus", "a+", "xaaa", 2);
    T("ere.full", "(a|b)+", "abba", 2);
    T("bre.brace", "a\\{2\\}", "aaa", 1);
    T("bre.plus", "a+", "a+", 1);
    T("bre.backref", "\\(a\\)\\1", "aa", 1);
    T("bre.group", "\\(a\\)b*", "abbb", 1);
    T("ext.group", "\\(a\\)b*", "abbb", 2);
    T("ext.brace", "a\\{2\\}", "aaa", 2);
    T("awk.alt", "a|b", "b", 4);
    T("awk.esc", "a\\/b", "a/b", 4);
    T("awk.group", "\\(a\\)", "a", 4);
    T("grep.group", "\\(a\\)", "a", 8);
    T("egrep.group", "\\(a\\)", "a", 16);
    T("grep.nl", "a\nb", "xb", 8);
    T("ecma.wordb", "\\bfoo\\b", "a foo b", 0);
    T("ecma.hex", "\\x41", "A", 0);
    T("ecma.unicode", "\\u0041", "A", 0);
    T("ecma.ctrl", "\\cJ", "\n", 0);
    T("ecma.nul", "\\0", "a", 0);
    T("ecma.badescape", "\\", "a", 0);
    T("ecma.identity", "\\q", "q", 0);
    T("ecma.digitclass", "[\\d]+", "a12", 0);
    T("ecma.notdigit", "\\D+", "12ab", 0);
    T("ecma.backref", "(a)\\1", "aa", 0);
    T("ecma.badbackref", "(a)\\2", "aa", 0);
    T("ecma.octal", "\\101", "A", 0);
    T("ecma.tab", "\\t", "\t", 0);
    T("ecma.esc.bracket", "\\[a\\]", "[a]", 0);
    PLog("rep.sed.amp", RegExpReplace("b", "abc", "[&\\&]", 0, 256));
    PLog("rep.sed.nested", RegExpReplace("(a)(b)", "ab", "\\2\\1\\\\x", 0, 256));
    PLog("rep.sed.dollar", RegExpReplace("(a)", "ab", "$1", 0, 256));
    PLog("rep.default.backslash", RegExpReplace("(a)", "ab", "\\1", 0, 0));
    PLog("rep.dollar.amp", RegExpReplace("b", "abc", "$&$&", 0, 0));
    PLog("rep.dollar.nn", RegExpReplace("(a)", "ab", "$01$001", 0, 0));
}
void main() { Go(); }
