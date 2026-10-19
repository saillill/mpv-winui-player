using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;
using Windows.Foundation;

namespace mpv_winui.Modules.Common.View;

/// <summary>
/// Vector icons taken verbatim from Microsoft's Fluent UI System Icons.
///
/// Sourced, not drawn. Every constant in <see cref="FluentIcons"/> is the
/// <c>d</c> attribute of a named file in <c>microsoft/fluentui-system-icons</c>
/// (24px, outline variant), carried across unchanged apart from the <c>F1</c>
/// fill-rule prefix that XAML's geometry mini-language wants. An icon path is
/// not something to approximate: a hand-written one renders as a
/// confident-looking wrong shape, and nothing in this project's checks would
/// notice.
///
/// To add one, download the SVG from the repository and add it to the generator
/// - do not redraw it.
///
/// GEOMETRY LIFETIME - the reason this file parses paths by hand.
///
/// A Geometry handed out here gets assigned to a PathIcon.Data by the caller,
/// and WinUI lets a geometry belong to exactly ONE PathIcon for its whole
/// life: a second assignment - to any other icon, and even to the same icon
/// after another geometry took over - fails with E_INVALIDARG ("Value does not
/// fall within the expected range"). A controlled lab on this machine pinned
/// that down, along with two neighbours: a geometry produced by
/// <c>XamlBindingHelper.ConvertValue</c> is subject to the same single-use
/// rule, and a Geometry created on a background thread cannot be assigned on
/// the UI thread at all.
///
/// So this helper has NO cache and returns a FRESH PathGeometry from every
/// call, built by parsing the mini-language directly (M/L/H/V/C/Z, absolute
/// and relative). Callers must call it on the UI thread and must never use
/// one returned instance for two assignments. In a data template that means
/// binding a path STRING and creating the geometry per binding application -
/// see LibraryEntry.MediaIcon for the pattern in practice.
///
/// A command outside M/L/H/V/C/Z throws instead of approximating: an icon that
/// needs arcs (A) or smooth curves (S/Q/T) fails loudly at the point of use,
/// rather than rendering as a confident-looking wrong shape.
///
/// <see cref="IconFonts"/> is the sibling of this class: the same icon set
/// delivered as a font, which is what the control bar, menu bar and settings
/// categories use. The font is right where an icon is a glyph in a control
/// that already has a FontFamily. Paths are right where the icon comes from
/// data rather than from the layout - a card whose media kind is a property of
/// the row cannot name a glyph in markup.
///
/// The constants live in FluentIcons.Gen.cs.
/// </summary>
public static partial class FluentIcons
{
    /// <summary>
    /// One of the constants above as a fresh <see cref="Geometry"/>, for
    /// assigning to a <c>PathIcon.Data</c>. Fresh per call - see the lifetime
    /// rule in the class comment.
    /// </summary>
    public static Geometry Geometry(string pathData) => Parse(pathData);

    /// <summary>
    /// A <see cref="PathIcon"/> for one of the constants above, sized to
    /// <paramref name="size"/>, for callers building rows in code. Each call
    /// builds its own geometry.
    /// </summary>
    public static PathIcon Create(string pathData, double size) => new()
    {
        Width = size,
        Height = size,
        Data = Geometry(pathData),
    };

    /// <summary>
    /// Parses the XAML path mini-language subset the Fluent icon set uses.
    /// See the class comment for why this exists rather than a conversion API.
    /// </summary>
    private static PathGeometry Parse(string pathData)
    {
        if (string.IsNullOrWhiteSpace(pathData))
        {
            throw new ArgumentException("Empty path data", nameof(pathData));
        }

        var figures = new PathFigureCollection();
        var fillRule = FillRule.Nonzero;
        int i = 0;
        int n = pathData.Length;

        void Skip()
        {
            while (i < n && (pathData[i] == ' ' || pathData[i] == '\t' || pathData[i] == '\r' || pathData[i] == '\n' || pathData[i] == ','))
            {
                i++;
            }
        }

        double Number()
        {
            Skip();
            int start = i;
            while (i < n && (char.IsDigit(pathData[i]) || pathData[i] == '-' || pathData[i] == '+' || pathData[i] == '.' || pathData[i] == 'e' || pathData[i] == 'E'))
            {
                i++;
            }

            if (i == start)
            {
                throw new FormatException($"Number expected at offset {start}");
            }

            return double.Parse(pathData.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // Optional leading fill rule, the "F0"/"F1" prefix.
        Skip();
        if (i + 1 < n && (pathData[i] == 'F' || pathData[i] == 'f'))
        {
            fillRule = pathData[i + 1] switch
            {
                '0' => FillRule.EvenOdd,
                '1' => FillRule.Nonzero,
                _ => throw new FormatException("The fill rule must be F0 or F1"),
            };
            i += 2;
        }

        char command = '\0';
        Point current = default;
        Point figureStart = default;
        PathFigure? figure = null;

        void AddLine(Point p)
        {
            if (figure is null)
            {
                throw new FormatException("A drawing command appeared before any M");
            }

            figure.Segments.Add(new LineSegment { Point = p });
            current = p;
        }

        while (true)
        {
            Skip();
            if (i >= n)
            {
                break;
            }

            char c = pathData[i];
            if (char.IsLetter(c))
            {
                command = c;
                i++;
            }
            else if (command == '\0')
            {
                throw new FormatException("Coordinates appeared before any command");
            }

            bool relative = char.IsLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                {
                    double x = Number(), y = Number();
                    var p = relative ? new Point(current.X + x, current.Y + y) : new Point(x, y);
                    figure = new PathFigure { StartPoint = p };
                    figures.Add(figure);
                    current = p;
                    figureStart = p;
                    // Repeats of M's coordinate pairs are implicit linetos.
                    command = relative ? 'l' : 'L';
                    break;
                }

                case 'L':
                {
                    double x = Number(), y = Number();
                    var p = relative ? new Point(current.X + x, current.Y + y) : new Point(x, y);
                    AddLine(p);
                    break;
                }

                case 'H':
                {
                    double x = Number();
                    AddLine(relative ? new Point(current.X + x, current.Y) : new Point(x, current.Y));
                    break;
                }

                case 'V':
                {
                    double y = Number();
                    AddLine(relative ? new Point(current.X, current.Y + y) : new Point(current.X, y));
                    break;
                }

                case 'C':
                {
                    if (figure is null)
                    {
                        throw new FormatException("A drawing command appeared before any M");
                    }

                    double x1 = Number(), y1 = Number(), x2 = Number(), y2 = Number(), x3 = Number(), y3 = Number();
                    if (relative)
                    {
                        x1 += current.X; y1 += current.Y;
                        x2 += current.X; y2 += current.Y;
                        x3 += current.X; y3 += current.Y;
                    }

                    figure.Segments.Add(new BezierSegment
                    {
                        Point1 = new Point(x1, y1),
                        Point2 = new Point(x2, y2),
                        Point3 = new Point(x3, y3),
                    });
                    current = new Point(x3, y3);
                    break;
                }

                case 'Z':
                {
                    if (figure is null)
                    {
                        throw new FormatException("Z appeared before any M");
                    }

                    figure.IsClosed = true;
                    current = figureStart;
                    // No arguments follow; a stray number is a format error.
                    command = '\0';
                    break;
                }

                default:
                    throw new NotSupportedException($"Unsupported path command '{command}'");
            }
        }

        if (figures.Count == 0)
        {
            throw new FormatException("The path data held no figures");
        }

        return new PathGeometry { Figures = figures, FillRule = fillRule };
    }
}
