using System.Globalization;

namespace Pupstrap.LiveFlags;

public sealed record FlagEntry(string Name, uint Rva);

public sealed class FlagDump
{
    public string Path { get; init; } = "";
    public string Dumper { get; set; } = "";
    public string RobloxVersion { get; set; } = "";
    public string DumperVersion { get; set; } = "";
    public string DumpedAt { get; set; } = "";
    public uint ListPointer { get; set; }
    public uint ListToFlag { get; set; }
    public uint ListToValue { get; set; }
    public List<FlagEntry> Flags { get; } = new();

    private Dictionary<string, FlagEntry>? _byName;
    public FlagEntry? Find(string name)
    {
        _byName ??= Flags.GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return _byName.GetValueOrDefault(name);
    }

    /// <summary>Reads the dumper's FFlags.hpp or FFlags.json / FFlagsHex.json output.</summary>
    public static FlagDump Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Dump file not found.", path);
        var dump = new FlagDump { Path = path };
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".hpp" or ".h") dump.LoadHeader(File.ReadLines(path));
        else dump.LoadJson(File.ReadLines(path));
        if (dump.Flags.Count == 0) throw new InvalidDataException("No flags found. Expected FFlags.hpp or FFlags.json from the dumper.");
        return dump;
    }

    private enum Section { Meta, List, Flags }

    private void LoadHeader(IEnumerable<string> lines)
    {
        var section = Section.Meta;
        foreach (var line in lines)
        {
            if (line.Contains("Dumped With")) Dumper = CommentValue(line);
            else if (line.Contains("Roblox Version")) RobloxVersion = CommentValue(line);
            else if (line.Contains("Dumper Version")) DumperVersion = CommentValue(line);
            else if (line.Contains("Dumped At")) DumpedAt = CommentValue(line);
            else if (line.Contains("ClientVersion") && RobloxVersion.Length == 0) RobloxVersion = QuotedString(line);

            if (line.Contains("namespace FFlagList")) { section = Section.List; continue; }
            if (line.Contains("namespace FFlags")) { section = Section.Flags; continue; }
            if (!line.Contains("uintptr_t") && line.Contains('}')) { section = Section.Meta; continue; }

            var typePos = line.IndexOf("uintptr_t ", StringComparison.Ordinal);
            if (typePos < 0) continue;
            var equals = line.IndexOf('=', typePos);
            if (equals < 0) continue;
            var name = line[(typePos + 10)..equals].Trim();
            var semi = line.IndexOf(';', equals);
            var token = semi < 0 ? line[(equals + 1)..] : line[(equals + 1)..semi];
            if (name.Length == 0 || !TryParseU32(token, out var value)) continue;

            if (section == Section.List) SetListField(name, value);
            else if (section == Section.Flags && value != 0) Flags.Add(new FlagEntry(name, value));
        }
    }

    private void LoadJson(IEnumerable<string> lines)
    {
        var section = Section.Meta;
        var depth = 0;
        foreach (var line in lines)
        {
            var key = JsonKey(line);
            if (section == Section.List)
            {
                if (line.Contains('{')) depth++;
                if (line.Contains('}') && --depth <= 0) { section = Section.Meta; continue; }
                if (TryParseAfterColon(line, out var listValue)) SetListField(key, listValue);
                continue;
            }
            if (key == "FFlagList") { section = Section.List; depth = 1; continue; }
            if (key == "FFlags") { section = Section.Flags; continue; }
            if (section == Section.Meta)
            {
                switch (key)
                {
                    case "Dumped With": Dumper = StringAfterColon(line); break;
                    case "Roblox Version": RobloxVersion = StringAfterColon(line); break;
                    case "Dumper Version": DumperVersion = StringAfterColon(line); break;
                    case "Dumped At": DumpedAt = StringAfterColon(line); break;
                }
                continue;
            }
            if (key.Length == 0) continue;
            if (TryParseAfterColon(line, out var offset) && offset != 0) Flags.Add(new FlagEntry(key, offset));
        }
    }

    private void SetListField(string name, uint value)
    {
        switch (name)
        {
            case "Pointer": ListPointer = value; break;
            case "ToFlag": ListToFlag = value; break;
            case "ToValue": ListToValue = value; break;
        }
    }

    private static string CommentValue(string line)
    {
        var colon = line.IndexOf(':');
        if (colon < 0) return "";
        var value = line[(colon + 1)..].Trim();
        if (value.EndsWith("*/")) value = value[..^2];
        return value.Trim();
    }

    private static string QuotedString(string line)
    {
        var open = line.IndexOf('"');
        var close = open < 0 ? -1 : line.IndexOf('"', open + 1);
        return close < 0 ? "" : line[(open + 1)..close];
    }

    private static string JsonKey(string line) => QuotedString(line);

    private static string StringAfterColon(string line)
    {
        var colon = line.IndexOf(':');
        return colon < 0 ? "" : QuotedString(line[(colon + 1)..]);
    }

    private static bool TryParseAfterColon(string line, out uint value)
    {
        value = 0;
        var colon = line.IndexOf(':');
        if (colon < 0) return false;
        var comma = line.IndexOf(',', colon);
        return TryParseU32(comma < 0 ? line[(colon + 1)..] : line[(colon + 1)..comma], out value);
    }

    private static bool TryParseU32(string token, out uint value)
    {
        token = token.Trim().Trim('"', ',', ' ');
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(token[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
