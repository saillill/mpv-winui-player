using mpv_winui.Modules.FileSystem;
using mpv_winui.Modules.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace mpv_winui.Modules.MpvConf.Conf;

public sealed class MpvConfManager
{
    public const string DefaultSectionName = "default";

    private readonly string _filePath;
    private readonly List<MpvConfLine> _lines = [];

    public MpvConfManager(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public bool IsLoaded
    {
        get;
        private set;
    }

    public IReadOnlyList<MpvConfLine> Lines => _lines.Where(l => l.Status != MpvConfLineStatus.Deleted).ToList();

    public IEnumerable<MpvConfLine> Options => _lines.Where(l => l.IsOption && l.Status != MpvConfLineStatus.Deleted);

    public IReadOnlyList<MpvConfLine> DeletedLines => _lines.Where(l => l.Status == MpvConfLineStatus.Deleted).ToList();

    public IReadOnlyList<string> Sections => _lines.Where(l => l.Type == MpvConfLineType.Section).Select(l => l.Section).Distinct(StringComparer.Ordinal).ToList();

    public void Load()
    {
        _lines.Clear();
        if (File.Exists(_filePath))
        {
            _lines.AddRange(MpvConfParser.Parse(File.ReadAllLines(_filePath)));
            Reindex();
        }

        IsLoaded = true;
    }

    public async Task SaveAsync(bool backup = false, int limit = 50)
    {
        var removedSections = _lines
            .Where(l => l.Type == MpvConfLineType.Section && l.SectionDeleted)
            .Select(l => l.Section)
            .ToHashSet(StringComparer.Ordinal);
        var present = _lines.Where(l => l.Status != MpvConfLineStatus.Deleted && !removedSections.Contains(l.Section)).ToList();
        var content = string.Join("\n", present.Select(l => l.Raw)) + "\n";
        // This editor writes the same file the settings window watches. Stamping
        // the write generation keeps the save from being read back as a hand
        // edit: without it the settings window would reload a moment after every
        // save here and log a change nobody made. Only when the two paths are the
        // same file - this manager can be pointed at a different mpv.conf, and a
        // write to some other file is nobody else's business.
        if (PathsEqual(_filePath, ManagedMpvConfig.MpvConfPath))
        {
            MpvConfWatcher.NoteOwnWrite();
        }

        await FileService.Instance.BackAndSaveAsync(_filePath, content, backup, limit).ConfigureAwait(false);

        _lines.Clear();
        _lines.AddRange(present);
        foreach (MpvConfLine line in _lines)
        {
            line.Modified = false;
            line.Status = MpvConfLineStatus.Existing;
        }

        Reindex();
    }

    /// <summary>Whether two paths name the same file.
    ///
    /// Compared resolved rather than as strings: the two callers build the path
    /// independently and can differ in separators, casing or a relative segment
    /// while still naming one file. A false negative here is a spurious reload
    /// of our own save; a false positive would be worse but is not reachable by
    /// construction, since one side is always the deployed config.</summary>
    private static bool PathsEqual(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // A malformed path cannot be the deployed config, which is always
            // well-formed, so "not the same file" is the safe answer.
            return false;
        }
    }

    public MpvConfLine? Get(string key, string? section = null)
    {
        return _lines.FirstOrDefault(l => l.IsOption && l.Status != MpvConfLineStatus.Deleted && string.Equals(l.Key, key, StringComparison.Ordinal) && (section is null || string.Equals(l.Section, section, StringComparison.Ordinal)));
    }

    public IReadOnlyList<MpvConfLine> GetAll(string key, string? section = null)
    {
        return _lines.Where(l => l.IsOption && l.Status != MpvConfLineStatus.Deleted && string.Equals(l.Key, key, StringComparison.Ordinal) && (section is null || string.Equals(l.Section, section, StringComparison.Ordinal))).ToList();
    }

    public bool ContainsSection(string section)
    {
        return _lines.Any(l => l.Type == MpvConfLineType.Section && string.Equals(l.Section, section, StringComparison.Ordinal));
    }

    public MpvConfLine InsertOption(string key, string value, string? section = null)
    {
        string target = section ?? string.Empty;

        MpvConfLine? deleted = _lines.FirstOrDefault(l => l.Status == MpvConfLineStatus.Deleted && l.IsOption && string.Equals(l.Key, key, StringComparison.Ordinal) && string.Equals(l.Section, target, StringComparison.Ordinal));
        if (deleted is not null)
        {
            deleted.Status = MpvConfLineStatus.Existing;
            deleted.Value = value;
            deleted.Enabled = true;
            deleted.Modified = true;
            return deleted;
        }

        if (target.Length > 0 && !ContainsSection(target))
        {
            _lines.Add(MpvConfLine.SectionLine($"[{target}]", target));
        }

        var option = MpvConfLine.Option(string.Empty, target, key, value, enabled: true, quoteChar: null, inlineComment: string.Empty);
        int insertAt = FindInsertIndex(target);
        _lines.Insert(insertAt, option);
        Reindex();
        return option;
    }

    public MpvConfLine InsertDisabled(string key, string value, string? section = null)
    {
        var option = InsertOption(key, value, section);
        option.Enabled = false;
        return option;
    }

    public bool Remove(MpvConfLine line)
    {
        if (!_lines.Contains(line))
        {
            return false;
        }

        if (line.Status == MpvConfLineStatus.Added)
        {
            _lines.Remove(line);
        }
        else if (line.Status == MpvConfLineStatus.Existing)
        {
            line.Status = MpvConfLineStatus.Deleted;
        }
        else
        {
            return false;
        }

        Reindex();
        return true;
    }

    public bool Restore(MpvConfLine line)
    {
        if (!_lines.Contains(line) || line.Status != MpvConfLineStatus.Deleted)
        {
            return false;
        }

        line.Status = MpvConfLineStatus.Existing;
        return true;
    }

    public void InsertSection(string section)
    {
        string target = section ?? string.Empty;
        if (target.Length > 0 && !ContainsSection(target))
        {
            _lines.Add(MpvConfLine.SectionLine($"[{target}]", target));
            Reindex();
        }
    }

    public bool IsSectionDeleted(string section)
    {
        var headers = _lines.Where(l => l.Type == MpvConfLineType.Section && string.Equals(l.Section, section, StringComparison.Ordinal)).ToList();
        return headers.Count > 0 && headers.All(l => l.SectionDeleted);
    }

    public bool RemoveSection(string section)
    {
        bool changed = false;
        foreach (MpvConfLine line in _lines)
        {
            if (line.Type == MpvConfLineType.Section && string.Equals(line.Section, section, StringComparison.Ordinal) && !line.SectionDeleted)
            {
                line.SectionDeleted = true;
                changed = true;
            }
        }

        return changed;
    }

    public bool RestoreSection(string section)
    {
        bool changed = false;
        foreach (MpvConfLine line in _lines)
        {
            if (line.Type == MpvConfLineType.Section && string.Equals(line.Section, section, StringComparison.Ordinal) && line.SectionDeleted)
            {
                line.SectionDeleted = false;
                changed = true;
            }
        }

        return changed;
    }

    public bool RenameSection(string oldName, string newName)
    {
        string target = newName?.Trim() ?? string.Empty;
        if (target.Length == 0
            || target == oldName
            || target == DefaultSectionName
            || !ContainsSection(oldName)
            || ContainsSection(target))
        {
            return false;
        }

        foreach (MpvConfLine line in _lines)
        {
            if (string.Equals(line.Section, oldName, StringComparison.Ordinal))
            {
                line.RetargetSection(target);
            }
        }

        return true;
    }

    private int FindInsertIndex(string section)
    {
        if (section.Length == 0)
        {
            int firstSection = _lines.FindIndex(l => l.Type == MpvConfLineType.Section);
            return firstSection < 0 ? _lines.Count : firstSection;
        }

        int lastInSection = _lines.FindLastIndex(l => l.Type == MpvConfLineType.Section && string.Equals(l.Section, section, StringComparison.Ordinal));

        if (lastInSection < 0)
        {
            return _lines.Count;
        }

        int index = lastInSection + 1;
        while (index < _lines.Count)
        {
            var line = _lines[index];
            if (line.Type == MpvConfLineType.Section)
            {
                break;
            }

            index++;
        }

        return index;
    }

    private void Reindex()
    {
        int number = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Status != MpvConfLineStatus.Deleted)
            {
                _lines[i].LineNumber = number++;
            }
        }
    }
}
