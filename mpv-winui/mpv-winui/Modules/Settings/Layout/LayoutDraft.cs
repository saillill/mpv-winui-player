using System;
using System.Collections.Generic;
using System.Text.Json;

namespace mpv_winui.Modules.Settings.Layout;

/// <summary>
/// Session-scoped draft/undo stack over <see cref="SettingsLayout"/>.
///
/// The customize mode edits the layout live, which is what makes the pane
/// update as you drag. That is fine while you are in the mode, but it also
/// means an accidental drop is already on disk before you can look at it.
/// This class gives the mode an explicit "apply / discard" boundary: every
/// mutation is snapshotted, the stack backs undo and redo, and the whole
/// session can be reverted to the layout it started from.
///
/// Snapshots are JSON, not object clones: the model is a plain serializable
/// graph, and round-tripping it through the same serializer the store uses
/// guarantees a draft can never carry a reference the live model still holds.
/// </summary>
internal sealed class LayoutDraft
{
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();

    private string _baseline = Empty;
    private string _current = Empty;
    private bool _suspended;

    /// <summary>Snapshot of an empty layout, used before Begin has been called.</summary>
    private const string Empty = "{}";

    /// <summary>Whether anything changed since the baseline was taken.</summary>
    internal bool IsDirty => !string.Equals(_baseline, _current, StringComparison.Ordinal);

    internal bool CanUndo => _undo.Count > 0;

    internal bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Records the layout as the point "discard" returns to. Called when the
    /// customize mode opens; re-taking the baseline mid-session would make a
    /// discard silently keep whatever was on disk.
    /// </summary>
    internal void Begin(SettingsLayout layout)
    {
        _baseline = Serialize(layout);
        _current = _baseline;
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>
    /// Snapshots the state *before* a mutation. Call immediately before
    /// changing <see cref="SettingsLayout"/>; the pair with <see cref="Commit"/>
    /// brackets one logical edit.
    /// </summary>
    internal void Push(SettingsLayout layout)
    {
        if (_suspended)
        {
            return;
        }

        _undo.Push(_current);
        _redo.Clear();

        // The pre-edit current is what undo restores; the post-edit state
        // arrives with Commit. Pushing the live layout here would make undo
        // restore the change it is meant to reverse.
        _current = Serialize(layout);
    }

    /// <summary>Records the state after a mutation, without pushing a new undo step.</summary>
    internal void Commit(SettingsLayout layout)
    {
        _current = Serialize(layout);
    }

    /// <summary>Applies one undo step to the live layout. Returns whether one was applied.</summary>
    internal bool Undo(SettingsLayout layout)
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        _redo.Push(_current);
        _current = _undo.Pop();
        Restore(layout, _current);
        return true;
    }

    /// <summary>Applies one redo step to the live layout. Returns whether one was applied.</summary>
    internal bool Redo(SettingsLayout layout)
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        _undo.Push(_current);
        _current = _redo.Pop();
        Restore(layout, _current);
        return true;
    }

    /// <summary>Restores the baseline (the state the mode opened with) as one undoable step.</summary>
    internal bool Discard(SettingsLayout layout)
    {
        if (!IsDirty)
        {
            return false;
        }

        _undo.Push(_current);
        _redo.Clear();
        _current = _baseline;
        Restore(layout, _current);
        return true;
    }

    /// <summary>
    /// Makes the current state the new baseline, so a later discard keeps it.
    /// Called after "apply", which is the user saying this is what they want.
    /// </summary>
    internal void AcceptBaseline()
    {
        _baseline = _current;
    }

    /// <summary>
    /// Runs a mutation without recording history, for changes the user did not
    /// initiate (a rebuild, or restoring the model after an undo).
    /// </summary>
    internal IDisposable Suspended() => new Scope(this);

    /// <summary>Copies a snapshot back into the live layout object, in place.</summary>
    private static void Restore(SettingsLayout layout, string snapshot)
    {
        SettingsLayout? restored;
        try
        {
            restored = JsonSerializer.Deserialize(snapshot, SettingsLayoutJsonContext.Default.SettingsLayout);
        }
        catch (JsonException)
        {
            return;
        }

        if (restored is null)
        {
            return;
        }

        // In place: the page and every control hold this exact instance.
        layout.Version = restored.Version;
        layout.Order = restored.Order ?? [];
        layout.Entries = restored.Entries ?? new Dictionary<string, SettingsLayoutEntry>(StringComparer.Ordinal);
        layout.CategoryOrder = restored.CategoryOrder ?? [];
        layout.HiddenCategories = restored.HiddenCategories ?? [];
        layout.Added = restored.Added ?? [];
        layout.SectionOrder = restored.SectionOrder ?? [];
        layout.HiddenSections = restored.HiddenSections ?? [];
        layout.CustomSections = restored.CustomSections ?? [];
        layout.CustomCategories = restored.CustomCategories ?? [];
    }

    private static string Serialize(SettingsLayout layout)
    {
        try
        {
            return JsonSerializer.Serialize(layout, SettingsLayoutJsonContext.Default.SettingsLayout);
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly LayoutDraft _owner;
        private readonly bool _previous;

        internal Scope(LayoutDraft owner)
        {
            _owner = owner;
            _previous = owner._suspended;
            owner._suspended = true;
        }

        public void Dispose() => _owner._suspended = _previous;
    }
}
