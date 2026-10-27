using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using mpv_winui.Modules.Common.View;
using mpv_winui.Domains.Settings;
using mpv_winui.Domains.SettingsModel;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace mpv_winui.Domains.MpvConf;

/// <summary>
/// An in-app editor for mpv.conf.
///
/// <para><b>Why this exists next to the shell's own editor.</b> Handing the file
/// to whatever .conf opens with is the shortest route to an edit, but it leaves
/// the user in Notepad with no idea which lines belong to the settings window and
/// no way to tell a comment from an option. This window is the same file with
/// that information drawn in: option names, values, comments and sections each in
/// their own colour, numbered lines, and the colours named in the bar below.</para>
///
/// <para><b>Saves itself.</b> There is no save button because there is nothing
/// for one to decide: the buffer is the file. Every pause in typing is written
/// through, so closing the window can never lose an edit.</para>
/// </summary>
public sealed partial class MpvConfTextWindow : BaseWindow
{
    // The familiar editor palette, on the dark surface this window always uses.
    private static readonly Color DefaultColor = Color.FromArgb(255, 0xD4, 0xD4, 0xD4);
    private static readonly Color CommentColor = Color.FromArgb(255, 0x6A, 0x99, 0x55);
    private static readonly Color SectionColor = Color.FromArgb(255, 0xDC, 0xDC, 0xAA);
    private static readonly Color KeyColor = Color.FromArgb(255, 0x9C, 0xDC, 0xFE);
    private static readonly Color ValueColor = Color.FromArgb(255, 0xCE, 0x91, 0x78);
    private static readonly Color FlagColor = Color.FromArgb(255, 0xC5, 0x86, 0xC0);

    private const string EditorFont = "Cascadia Mono";
    private const float EditorFontSize = 12f;
    private const double GutterNumberFontSize = 11;

    /// <summary>Re-colours after a pause in typing rather than on every keystroke.</summary>
    private readonly DispatcherQueueTimer _recolor;

    /// <summary>Writes the file after a pause in typing. This is the save.</summary>
    private readonly DispatcherQueueTimer _autosave;

    /// <summary>Gutter labels, kept and repositioned rather than rebuilt.</summary>
    private readonly List<TextBlock> _numbers = [];

    private ScrollViewer? _editorScroll;

    // ---- Gutter geometry -------------------------------------------------
    //
    // Measured once per load and then used arithmetically, deliberately. The
    // document's own positions (ITextRange.GetPoint) can only be asked for
    // safely outside RichEdit's change and scroll notifications -- calling into
    // the text object model while RichEdit is dispatching one of those re-enters
    // its layout and takes the process down with an access violation inside
    // coreclr. So the scroll path reads a plain number (VerticalOffset) and does
    // arithmetic; the text object model is only touched on load and on the
    // colouring pass, both of which are ordinary contexts.

    private int _lineCount = 1;
    private double _lineHeight;
    private double _textTop;
    private bool _guttering;

    /// <summary>The lines the document currently shows coloured, so a keystroke
    /// only repaints the one line it changed instead of all of them. Entries are
    /// filled in as they are painted, which is also how a pass knows where to
    /// resume.</summary>
    private string[]? _paintedLines;

    /// <summary>True when the buffer differs from the file on disk.</summary>
    private bool _dirty;

    /// <summary>True while the document is being replaced, so its own change
    /// notification is not mistaken for the user typing.</summary>
    private bool _loading;

    /// <summary>True inside <see cref="Recolor"/>. Applying character formats can
    /// raise TextChanged on some RichEdit builds; without this the handler would
    /// re-arm the timer that just fired.</summary>
    private bool _recoloring;

    /// <summary>
    /// Set by <see cref="ReloadFromDisk"/> and consumed by the first change
    /// notification, which is the one the load itself raises. Recognising it
    /// that way rather than by comparing text keeps a document read out of the
    /// notification path.
    /// </summary>
    private bool _expectLoadEcho;

    private bool _loadedOnce;

    public MpvConfTextWindow()
    {
        InitializeComponent();

        AppWindow.Title = "mpv.conf";
        AppWindow.SetIcon("App.ico");
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

        // The strip this window extends into is dark whatever the app theme is,
        // so the caption has to be told about it twice: light glyphs, and a
        // transparent background. Without the background the three buttons keep
        // the light-mode title-bar colour and sit on the dark bar as a separate
        // floating chip.
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = Color.FromArgb(255, 0xCC, 0xCC, 0xCC);
        titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 0x88, 0x88, 0x88);
        titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF);
        titleBar.ButtonHoverForegroundColor = Colors.White;
        titleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF);
        titleBar.ButtonPressedForegroundColor = Colors.White;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 560;
            presenter.PreferredMinimumHeight = 380;
        }

        SizeToWorkArea();

        _recolor = DispatcherQueue.CreateTimer();
        _recolor.Interval = TimeSpan.FromMilliseconds(180);
        _recolor.IsRepeating = false;
        _recolor.Tick += (_, _) => Recolor();

        _autosave = DispatcherQueue.CreateTimer();
        _autosave.Interval = TimeSpan.FromMilliseconds(700);
        _autosave.IsRepeating = false;
        _autosave.Tick += (_, _) => Save();

        // No KeyboardAccelerator for Ctrl+S. Registering one makes WinUI draw an
        // accelerator tooltip, which is the "Ctrl+S" card that followed the
        // pointer around -- and it would be advertising a key with nothing to do
        // here, because the file is written on every pause and there is no
        // unsaved state for a shortcut to flush. What this window does need is
        // the opposite guarantee: closing it must not outrun the pending write.
        Closed += (_, _) => FlushPendingSave();

        // TEMP EXPERIMENT: opacity gating removed to see whether hiding the
        // control is what stops RichEdit from rendering the applied colours.

        ApplyLabels();

        // Loaded on first activation rather than in the constructor: the
        // RichEditBox's document is only reliably ready once the window has been
        // activated, and loading into it earlier silently drops the text.
        Activated += MpvConfTextWindow_Activated;
    }

    /// <summary>Refreshes the strings that are not baked into the markup.</summary>
    public void ApplyLabels()
    {
        var lang = AppContext.AppLang;
        LegendKeyText.Text = lang.MpvConfLegendKey;
        LegendValueText.Text = lang.MpvConfLegendValue;
        LegendCommentText.Text = lang.MpvConfLegendComment;
        LegendSectionText.Text = lang.MpvConfLegendSection;
    }

    private void MpvConfTextWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_loadedOnce)
        {
            return;
        }

        _loadedOnce = true;
        Activated -= MpvConfTextWindow_Activated;
        ReloadFromDisk();
        HookEditorScroll();
    }

    /// <summary>
    /// Finds the ScrollViewer inside the RichEditBox so the gutter can follow it.
    /// Its VerticalOffset is a plain property read, which is the whole point:
    /// that is safe to do while RichEdit is dispatching a scroll.
    /// </summary>
    private void HookEditorScroll()
    {
        if (_editorScroll is not null)
        {
            return;
        }

        _editorScroll = FindDescendant<ScrollViewer>(Editor);
        if (_editorScroll is not null)
        {
            _editorScroll.ViewChanged += (_, _) => UpdateGutter();
        }
    }

    private static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null)
        {
            return null;
        }

        int count;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(root);
        }
        catch
        {
            return null;
        }

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var deeper = FindDescendant<T>(child);
            if (deeper is not null)
            {
                return deeper;
            }
        }

        return null;
    }

    /// <summary>Re-reads the file from disk, discarding anything unsaved here.</summary>
    public void ReloadFromDisk()
    {
        var path = ManagedMpvConfig.MpvConfPath;
        PathText.Text = path;
        ToolTipService.SetToolTip(PathText, path);

        var text = string.Empty;
        try
        {
            if (File.Exists(path))
            {
                text = File.ReadAllText(path);
            }
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "read mpv.conf failed");
            SetStatus(ex.Message);
        }

        // RichEdit stores line breaks as CR and GetText hands them back that way,
        // so normalising here is what keeps the offsets Recolor computes pointing
        // at the characters it means.
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r");

        _loading = true;
        try
        {
            // Colour and font as the document's DEFAULT format, set before the
            // text goes in so the loaded lines inherit it.
            //
            // This is where the base has to live, for two reasons. Text the user
            // types later has to inherit it, and a range covering the whole
            // document is applied by RichEdit after the fact, so it can land on
            // top of the token colours and erase them.
            var baseFormat = Editor.Document.GetDefaultCharacterFormat();
            baseFormat.Name = EditorFont;
            baseFormat.Size = EditorFontSize;
            baseFormat.ForegroundColor = DefaultColor;
            Editor.Document.SetDefaultCharacterFormat(baseFormat);

            Editor.Document.SetText(TextSetOptions.None, text);
            // SetText leaves the caret at the end, which would open the window
            // scrolled to the bottom of the file. A config file is read from the
            // beginning, and having the view at the top is also what lets the
            // gutter be measured without a scroll offset in it.
            Editor.Document.Selection.SetRange(0, 0);
        }
        finally
        {
            _loading = false;
        }

        _expectLoadEcho = true;

        // SetText replaces the character formats along with the text, so the
        // record is stale: the next pass treats every line as changed.
        _paintedLines = null;

        Recolor();

        // Measure after the view has settled at the top: the point is asked for
        // in client coordinates, and at offset 0 none of them can be clamped.
        DispatcherQueue.TryEnqueue(() =>
        {
            MeasureTextMetrics();
            UpdateGutter();
        });
    }

    private void Editor_TextChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || _recoloring)
        {
            return;
        }

        // RichEdit raises this once for the programmatic load too. It is not
        // typing, and treating it as such erased the "saved" line every time the
        // window opened and immediately rewrote a file nobody had touched.
        if (_expectLoadEcho)
        {
            _expectLoadEcho = false;
            return;
        }

        // The file is now behind the buffer, so the bar stops claiming they agree
        // until the write lands.
        SetStatus(string.Empty);
        _dirty = true;

        _recolor.Stop();
        _recolor.Start();

        _autosave.Stop();
        _autosave.Start();
    }

    /// <summary>
    /// Writes a pending auto-save before the window goes away.
    ///
    /// <para>Auto-save is debounced, so closing the window inside that delay
    /// would otherwise drop the last edit -- the one case where "it saves itself"
    /// would not actually be true.</para>
    /// </summary>
    private void FlushPendingSave()
    {
        _autosave.Stop();
        if (_dirty)
        {
            Save();
        }
    }

    /// <summary>Writes the buffer to mpv.conf. This is the only save path.</summary>
    private void Save()
    {
        try
        {
            Editor.Document.GetText(TextGetOptions.None, out var text);

            // Back to how it will sit on disk, and drop the trailing break
            // RichEdit always keeps at the end of the document.
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (text.EndsWith('\n'))
            {
                text = text[..^1];
            }

            File.WriteAllText(ManagedMpvConfig.MpvConfPath, text);
            _dirty = false;

            // Every settings row is built from this file, so the "owned by a hand
            // edit" verdict has to be recomputed now or the rows keep the state
            // they had before the write.
            MpvConfOverrides.Refresh();
            SetStatus(string.Empty);
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "save mpv.conf failed");
            SetStatus(ex.Message);
        }
    }

    /// <summary>
    /// Puts a line in the status bar, or clears it.
    ///
    /// <para>Only failures are reported here. A successful save deliberately says
    /// nothing: the file writes itself every time typing pauses, so "saved" is the
    /// normal state rather than news, and a line that is always on screen reading
    /// "saved" is just a green decoration. Anything worth interrupting for is by
    /// definition the case where it did not save.</para>
    /// </summary>
    private void SetStatus(string text)
    {
        StatusText.Text = text;
    }

    private void GutterHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // The numbers live in the text's coordinates, so one that belongs to a
        // line outside the viewport has to be clipped rather than drawn over the
        // bars above and below.
        GutterHost.Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
        };

        UpdateGutter();
    }

    /// <summary>
    /// Re-applies the token colours to the whole document, and refreshes the line
    /// count the gutter works from.
    ///
    /// <para>Only character formats are touched, never the text: setting a format
    /// does not move the caret, which is what lets this run while the user is
    /// typing. The whole document is reset to the default colour first, because
    /// after an edit the previously painted ranges cover different characters
    /// than they did.</para>
    /// </summary>
    private void Recolor()
    {
        if (_recoloring)
        {
            return;
        }

        _recoloring = true;
        try
        {
            var doc = Editor.Document;
            doc.GetText(TextGetOptions.None, out var text);
            text ??= string.Empty;

            string[] lines = text.Length == 0 ? [string.Empty] : text.Split('\r');
            var lineCount = lines.Length;
            if (text.Length > 0 && text.EndsWith('\r'))
            {
                // GetText's trailing break is not a line the user typed.
                lineCount--;
            }

            _lineCount = Math.Max(1, lineCount);

            // A change in the number of lines shifts every index after it, so the
            // record is rebuilt from scratch -- with nulls, which read as "not
            // painted yet".
            if (_paintedLines is null || _paintedLines.Length != lines.Length)
            {
                _paintedLines = new string[lines.Length];
            }

            // Only tokens are written here. The base colour and the font are the
            // document's default format (see ReloadFromDisk), which is also what
            // keeps the control's own Foreground out of the picture: a control
            // Foreground overrides per-character colours, which is why every line
            // rendered as one flat grey no matter what was applied here.
            // Only lines whose text changed are repainted, so typing costs one
            // line rather than the file. These writes are the only ones after the
            // base format above, which is what lets them win.
            var start = 0;
            for (var i = 0; i < lineCount; i++)
            {
                var line = lines[i];
                if (!string.Equals(_paintedLines[i], line, StringComparison.Ordinal))
                {
                    PaintLine(doc, line, start);
                    _paintedLines[i] = line;
                }

                start += line.Length + 1;   // the CR that Split removed
            }

            UpdateGutter();
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "recolour mpv.conf failed");
        }
        finally
        {
            _recoloring = false;
        }
    }

    private static void PaintLine(RichEditTextDocument doc, string line, int start)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            return;
        }

        var lead = line.Length - trimmed.Length;

        // A comment owns the whole line, including any '=' it contains.
        if (trimmed[0] == '#')
        {
            Paint(doc, start + lead, line.Length - lead, CommentColor);
            return;
        }

        if (trimmed[0] == '[')
        {
            Paint(doc, start + lead, line.Length - lead, SectionColor);
            return;
        }

        var equals = line.IndexOf('=');
        if (equals < 0)
        {
            // A bare name: mpv reads it as "set this flag".
            Paint(doc, start + lead, line.Length - lead, FlagColor);
            return;
        }

        Paint(doc, start + lead, equals - lead, KeyColor);
        Paint(doc, start + equals + 1, line.Length - equals - 1, ValueColor);
    }

    private static void Paint(RichEditTextDocument doc, int start, int length, Color color)
    {
        if (length <= 0 || start < 0)
        {
            return;
        }

        doc.GetRange(start, start + length).CharacterFormat.ForegroundColor = color;
    }

    /// <summary>
    /// Asks the document where line 1 starts and how tall a line is.
    ///
    /// <para>Done once per load, with the view at the top, so nothing is clamped:
    /// the line height depends on the font Windows actually resolves for
    /// "Cascadia Mono" plus the paragraph spacing, which is not something the
    /// font size alone predicts.</para>
    /// </summary>
    private void MeasureTextMetrics()
    {
        try
        {
            var doc = Editor.Document;
            const PointOptions where = PointOptions.ClientCoordinates;

            doc.GetRange(0, 0).GetPoint(
                HorizontalCharacterAlignment.Left,
                VerticalCharacterAlignment.Top,
                where,
                out var first);
            _textTop = first.Y;

            if (_lineCount > 1)
            {
                doc.GetRange(1, 1).GetPoint(
                    HorizontalCharacterAlignment.Left,
                    VerticalCharacterAlignment.Top,
                    where,
                    out var second);
                var height = second.Y - first.Y;
                if (height > 1)
                {
                    _lineHeight = height;
                }
            }

            if (_lineHeight <= 0)
            {
                _lineHeight = EditorFontSize * 1.4;
            }
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "measure mpv.conf text metrics failed");
            if (_lineHeight <= 0)
            {
                _lineHeight = EditorFontSize * 1.4;
            }
        }
    }

    /// <summary>
    /// Numbers the visible lines.
    ///
    /// <para>Arithmetic only: the text object model is not touched here, because
    /// this runs from the editor's own scroll notification and re-entering
    /// RichEdit from there crashes the process.</para>
    /// </summary>
    private void UpdateGutter()
    {
        if (!_loadedOnce || _guttering)
        {
            return;
        }

        _guttering = true;
        try
        {
            var lineHeight = _lineHeight > 0 ? _lineHeight : EditorFontSize * 1.4;
            var offset = _editorScroll?.VerticalOffset ?? 0;

            // Where line 1 would be with the view at the top, less how far the
            // view has scrolled since.
            var origin = _textTop - offset;

            var startLine = origin < 0
                ? Math.Max(0, (int)Math.Floor(-origin / lineHeight))
                : 0;

            var bottom = Editor.ActualHeight;
            var slot = 0;
            for (var line = startLine; line < _lineCount; line++)
            {
                var y = origin + (line * lineHeight);
                if (y > bottom)
                {
                    break;
                }

                var label = LabelAt(slot++);
                label.Text = (line + 1).ToString();
                label.Height = lineHeight;
                label.LineHeight = lineHeight;
                Canvas.SetTop(label, y);
            }

            for (; slot < _numbers.Count; slot++)
            {
                _numbers[slot].Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "update line numbers failed");
        }
        finally
        {
            _guttering = false;
        }
    }

    private TextBlock LabelAt(int index)
    {
        while (_numbers.Count <= index)
        {
            var label = new TextBlock
            {
                // 46px of gutter, less the 1px seam and the 8px of air between
                // the numbers and the text.
                Width = 37,
                FontFamily = new FontFamily(EditorFont),
                FontSize = GutterNumberFontSize,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0x85, 0x85, 0x85)),
                TextAlignment = TextAlignment.Right,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                IsHitTestVisible = false,
            };
            _numbers.Add(label);
            Gutter.Children.Add(label);
        }

        var existing = _numbers[index];
        existing.Visibility = Visibility.Visible;
        return existing;
    }

    private void SizeToWorkArea()
    {
        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var width = Math.Max(640, (int)(area.Width * 0.5));
            var height = Math.Max(420, (int)(area.Height * 0.7));
            AppWindow.MoveAndResize(new RectInt32(
                area.X + ((area.Width - width) / 2),
                area.Y + ((area.Height - height) / 2),
                width,
                height));
        }
        catch (Exception ex)
        {
            AppContext.AppLogger.Error(ex, "size mpv.conf window failed");
        }
    }
}
