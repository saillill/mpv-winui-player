using mpv_winui.Platform.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace mpv_winui.Domains.MpvConf.Conf;

public sealed class MpvConfManager
{
    public const string DefaultSectionName = "default";

    private readonly string _filePath;
    private readonly Action? _noteOwnWrite;
    private readonly List<MpvConfLine> _lines = [];

    /// <summary>
    /// A reader/writer for one mpv.conf.
    ///
    /// <para><paramref name="onBeforeSave"/> is called just before the file is
    /// written, and exists because the settings window watches this same file:
    /// stamping a write generation first is what stops the save from being read
    /// back as a hand edit, which would make the settings window reload and log a
    /// change nobody made.</para>
    ///
    /// <para>It is a callback rather than a direct call to the watcher because this
    /// is the generic config editor — it can be pointed at any file, and knowing
    /// which files the settings window happens to watch is not its business. The
    /// composition root decides (see <c>MpvConfEditorPage</c>, which passes it only
    /// when the path is the app's own mpv.conf).</para>
    ///
    /// <para><b>That inversion is load-bearing, and its absence broke the test
    /// project.</b> The call used to be an inline check against
    /// <c>ManagedMpvConfig.MpvConfPath</c> followed by <c>MpvConfWatcher.NoteOwnWrite()</c>,
    /// which gave this class a compile-time dependency on
    /// <c>Domains.SettingsModel</c> — and therefore on <c>AppContext</c> and WinUI.
    /// <c>mpv-conf-test</c> links this file but not that module, so the whole suite
    /// stopped compiling, and two shipped defects (an escaped value that doubled
    /// on every launch until mpv.conf reached 896 MB, and six settings whose own
    /// written values could not be read back) went unnoticed because no test could
    /// run. See docs/settings-contract.md §4.1.</para>
    /// </summary>
    public MpvConfManager(string filePath, Action? onBeforeSave = null)
    {
        _filePath = filePath;
        _noteOwnWrite = onBeforeSave;
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
        // Tell whoever is watching this file that the next change is ours, before
        // it happens. The caller decides whether anything watches it at all; see
        // the constructor.
        _noteOwnWrite?.Invoke();

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
    /// <summary>Whether two paths name the same file, resolved rather than
    /// compared as strings.
    ///
    /// Compared resolved rather than as strings: the two callers build the path
    /// independently and can differ in separators, casing or a relative segment
    /// while still naming one file. A false negative here is a spurious reload
    /// of our own save; a false positive would be worse but is not reachable by
    /// construction, since one side is always the deployed config.
    ///
    /// <para>Internal rather than private because the composition root needs the
    /// same verdict to decide whether to pass an <c>onBeforeSave</c> callback at
    /// all — and it must be the same verdict, not a second implementation of it.
    /// See the constructor.</para></summary>
    internal static bool PathsEqual(string? a, string? b)
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
