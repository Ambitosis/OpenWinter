using System.Globalization;
using OpenWinter.Content;

namespace OpenWinter.Aurora.Formats;

/// <summary>
/// Material files (MTR): plain text, one keyword per line, "//" comments.
/// <code>
/// customshaderVS vslit
/// customshaderFS fswet
/// renderhint NormalAndSpecMapped
/// transparency 1
/// texture0 tno01_grass
/// parameter float wetDarken 0.20
/// parameter int mode 2
/// parameter float tint 1.0 0.5 0.25 1.0
/// </code>
/// Keywords are case-insensitive; a key may also be written "key=value".
/// </summary>
public static class Mtr
{
    public static MaterialData Parse(byte[] data, string name = "") => Parse(Text.Windows1252.GetString(data), name);

    public static MaterialData Parse(string text, string name = "")
    {
        var m = new MaterialData { Name = name };
        foreach (var raw in text.Split('\n'))
        {
            var line = raw;
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0) line = line[..comment];
            line = line.Replace('=', ' ').Replace('\t', ' ').Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string key = parts[0].ToLowerInvariant();
            string value = parts.Length > 1 ? parts[1] : "";
            switch (key)
            {
                case "customshadervs": m.VertexShader = value; break;
                case "customshaderfs": m.FragmentShader = value; break;
                case "customshadergs": m.GeometryShader = value; break;
                case "renderhint": m.RenderHint = value; break;
                case "transparency": m.Transparency = value is "1" or "true" or "TRUE" or "True"; break;
                case "parameter" when parts.Length >= 4:
                {
                    string type = parts[1].ToLowerInvariant();
                    var values = parts.Skip(3).Select(Number).ToArray();
                    int i = int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : (int)values[0];
                    m.Parameters.Add(new MaterialParameter(parts[2], type, values, i));
                    break;
                }
                default:
                    if (key.Length == 8 && key.StartsWith("texture", StringComparison.Ordinal) && char.IsAsciiDigit(key[7]) && key[7] - '0' < MaterialData.TextureSlots)
                        m.Textures[key[7] - '0'] = value.Equals("null", StringComparison.OrdinalIgnoreCase) ? "" : value;
                    else m.Unknown.Add(line);
                    break;
            }
        }
        return m;
    }

    static float Number(string s) =>
        float.TryParse(s.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
}
