using System.Text;

namespace OpenWinter.Aurora;

/// <summary>Text encodings used by Aurora data.</summary>
public static class Text
{
    /// <summary>Windows-1252: the de-facto encoding of strings in Aurora files and compiled scripts.</summary>
    public static Encoding Windows1252 => Bin.Text;
}
