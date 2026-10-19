using System.Buffers.Binary;
using Godot;

namespace OpenWinter.Client.Audio;

/// <summary>
/// Decodes WAVE files Godot can't load directly: IMA ADPCM (0x11) and
/// Microsoft ADPCM (0x02), to 16-bit PCM.
/// </summary>
public static class WavDecoder
{
    public static AudioStreamWav? Decode(byte[] wav)
    {
        int fmt = Find(wav, "fmt "u8), data = Find(wav, "data"u8);
        if (fmt < 0 || data < 0) return null;
        var f = wav.AsSpan(fmt + 8);
        int format = BinaryPrimitives.ReadUInt16LittleEndian(f);
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(f[2..]);
        int rate = BinaryPrimitives.ReadInt32LittleEndian(f[4..]);
        int blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(f[12..]);
        int dataLen = Math.Min(BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(data + 4)), wav.Length - data - 8);
        var payload = wav.AsSpan(data + 8, dataLen);
        short[]? pcm = format switch
        {
            0x11 => Ima(payload, channels, blockAlign),
            0x02 => MsAdpcm(payload, channels, blockAlign, f),
            _ => null,
        };
        if (pcm == null) return null;
        var bytes = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = channels == 2, Data = bytes };
    }

    static int Find(byte[] d, ReadOnlySpan<byte> tag) => d.AsSpan(0, Math.Min(d.Length, 4096)).IndexOf(tag);

    static readonly int[] ImaIndex = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
    static readonly int[] ImaStep =
    {
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
        130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060,
        1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484,
        7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767,
    };

    static short[] Ima(ReadOnlySpan<byte> src, int channels, int blockAlign)
    {
        var output = new List<short>(src.Length * 2);
        for (int block = 0; block + blockAlign <= src.Length; block += blockAlign)
        {
            var b = src.Slice(block, blockAlign);
            var pred = new int[channels];
            var index = new int[channels];
            for (int c = 0; c < channels; c++)
            {
                pred[c] = BinaryPrimitives.ReadInt16LittleEndian(b[(c * 4)..]);
                index[c] = Math.Clamp((int)b[c * 4 + 2], 0, 88);
            }
            int start = output.Count;
            for (int c = 0; c < channels; c++) output.Add((short)pred[c]);
            var body = b[(4 * channels)..];
            // Data comes in 4-byte groups per channel (8 samples each), interleaved by channel.
            int groups = body.Length / (4 * channels);
            var samples = new short[channels][];
            for (int c = 0; c < channels; c++) samples[c] = new short[groups * 8];
            for (int g = 0; g < groups; g++)
                for (int c = 0; c < channels; c++)
                    for (int k = 0; k < 4; k++)
                    {
                        byte v = body[(g * channels + c) * 4 + k];
                        samples[c][g * 8 + k * 2] = Step(ref pred[c], ref index[c], v & 0x0F);
                        samples[c][g * 8 + k * 2 + 1] = Step(ref pred[c], ref index[c], v >> 4);
                    }
            for (int i = 0; i < groups * 8; i++)
                for (int c = 0; c < channels; c++) output.Add(samples[c][i]);
        }
        return output.ToArray();
    }

    static short Step(ref int pred, ref int index, int nibble)
    {
        int step = ImaStep[index];
        int diff = step >> 3;
        if ((nibble & 1) != 0) diff += step >> 2;
        if ((nibble & 2) != 0) diff += step >> 1;
        if ((nibble & 4) != 0) diff += step;
        if ((nibble & 8) != 0) diff = -diff;
        pred = Math.Clamp(pred + diff, -32768, 32767);
        index = Math.Clamp(index + ImaIndex[nibble], 0, 88);
        return (short)pred;
    }

    static readonly int[] MsAdapt = { 230, 230, 230, 230, 307, 409, 512, 614, 768, 614, 512, 409, 307, 230, 230, 230 };
    static readonly int[,] MsCoef = { { 256, 0 }, { 512, -256 }, { 0, 0 }, { 192, 64 }, { 240, 0 }, { 460, -208 }, { 392, -232 } };

    static short[] MsAdpcm(ReadOnlySpan<byte> src, int channels, int blockAlign, ReadOnlySpan<byte> fmt)
    {
        var output = new List<short>(src.Length * 2);
        for (int block = 0; block + blockAlign <= src.Length; block += blockAlign)
        {
            var b = src.Slice(block, blockAlign);
            var coef = new int[channels, 2];
            var delta = new int[channels];
            var s1 = new int[channels];
            var s2 = new int[channels];
            int p = 0;
            for (int c = 0; c < channels; c++) { int pi = Math.Min((int)b[p++], 6); coef[c, 0] = MsCoef[pi, 0]; coef[c, 1] = MsCoef[pi, 1]; }
            for (int c = 0; c < channels; c++) { delta[c] = BinaryPrimitives.ReadInt16LittleEndian(b[p..]); p += 2; }
            for (int c = 0; c < channels; c++) { s1[c] = BinaryPrimitives.ReadInt16LittleEndian(b[p..]); p += 2; }
            for (int c = 0; c < channels; c++) { s2[c] = BinaryPrimitives.ReadInt16LittleEndian(b[p..]); p += 2; }
            for (int c = 0; c < channels; c++) output.Add((short)s2[c]);
            for (int c = 0; c < channels; c++) output.Add((short)s1[c]);
            int ch = 0;
            for (; p < b.Length; p++)
                foreach (int nib in new[] { b[p] >> 4, b[p] & 0x0F })
                {
                    int signed = nib >= 8 ? nib - 16 : nib;
                    int predicted = (s1[ch] * coef[ch, 0] + s2[ch] * coef[ch, 1]) >> 8;
                    int sample = Math.Clamp(predicted + signed * delta[ch], -32768, 32767);
                    output.Add((short)sample);
                    s2[ch] = s1[ch];
                    s1[ch] = sample;
                    delta[ch] = Math.Max(16, MsAdapt[nib] * delta[ch] >> 8);
                    ch = (ch + 1) % channels;
                }
        }
        return output.ToArray();
    }
}
