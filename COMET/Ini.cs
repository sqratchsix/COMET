using System;
using System.Collections.Generic;
using System.Linq;

internal class Ini
{
    private readonly Dictionary<string, Dictionary<string, string>> _ini = new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);
    private string CurrentSection { get; set; } = "";

    public Ini(string iniContents)
    {
        var currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        _ini[""] = currentSection;

        foreach (var line in iniContents.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                               .Where(t => !string.IsNullOrWhiteSpace(t))
                               .Select(t => t.Trim()))
        {
            if (line.StartsWith(";") || line.StartsWith("#"))
                continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                _ini[line.Substring(1, line.LastIndexOf("]", StringComparison.Ordinal) - 1)] = currentSection;
                continue;
            }

            var idx = line.IndexOf("=", StringComparison.Ordinal);
            if (idx == -1)
                currentSection[line] = "";
            else
            {
                var key = line.Substring(0, idx);
                key = key.TrimEnd(' ');
                var val = line.Substring(idx + 1);
                val = val.TrimStart(' ');
                currentSection[key] = line.Substring(idx + 1);
            }
        }
    }

    public string GetValue(string key)
    {
        return GetValue(key, CurrentSection, "");
    }

    public string GetValue(string key, string section, string @default = "")
    {
        if (!_ini.ContainsKey(section))
            return @default;

        return !_ini[section].ContainsKey(key) ? @default : _ini[section][key];
    }

    public string[] GetKeys(string section) => !_ini.ContainsKey(section) ? new string[0] : _ini[section].Keys.ToArray();

    public Ini Section(string section)
    {
        CurrentSection = section;
        return this;
    }

    public bool Exists(string section) => Sections.Contains(section);
    public bool Exists(string section, string key) => _ini?[section].Keys.Contains(key) ?? false;

    public string[] Sections => _ini.Keys.Where(t => t != "").ToArray();

    public string[] Keys => _ini?[CurrentSection].Keys.ToArray();
}
