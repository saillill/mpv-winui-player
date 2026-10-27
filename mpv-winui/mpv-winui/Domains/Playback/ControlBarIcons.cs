namespace mpv_winui.Domains.Playback;

/// <summary>Shared icon data used by both the real control bar and the settings previews.</summary>
public static class ControlBarIcons
{
    /// <summary>The generated Fluent System Icons ArrowShuffle - the shape the
    /// control bar's shuffle button draws.
    ///
    /// The two control-bar style templates cannot x:Bind a C# constant, so each
    /// carries its own copy of this path (see the note above ShuffleStates in
    /// CenterPlayerControlStyle.xaml). This constant therefore has no consumer
    /// in code today; it is kept as the C# handle for the glyph so the next
    /// consumer does not have to re-derive it.</summary>
    public const string Shuffle = mpv_winui.Infrastructure.Common.View.FluentIcons.ArrowShuffle;

    /// <summary>
    /// Fluent "Picture in picture" path (Microsoft Fluent UI System Icons,
    /// picture_in_picture_24_filled), matching the button that toggles the
    /// compact PiP control bar.
    /// </summary>
    public const string PictureInPicture =
        "F1 M 5.25 3 C 4.679548 3 4.119026 3.150192 3.625 3.435417 C 3.130974 3.720643 2.720643 4.130974 2.435417 4.625 C 2.150192 5.119026 2 5.679548 2 6.25 L 2 15.75 C 2 17.55 3.46 19 5.25 19 L 11 19 L 11 15 C 11 14.473429 11.138638 13.956024 11.401924 13.5 C 11.665209 13.043976 12.043976 12.665209 12.5 12.401924 C 12.956024 12.138638 13.473429 12 14 12 L 21 12 C 21.340562 11.999495 21.678737 12.056984 22 12.17 L 22 6.25 C 22 4.45 20.54 3 18.75 3 Z M 22 13.27 C 21.848734 13.182253 21.686611 13.114722 21.517783 13.069139 C 21.348954 13.023555 21.174874 13.00031 21 13 L 14 13 C 13.648953 13 13.304016 13.092426 13 13.267949 C 12.695984 13.443473 12.443473 13.695984 12.267949 14 C 12.092426 14.304016 12 14.648953 12 15 L 12 20 C 12 21.1 12.9 22 14 22 L 21 22 C 21.351047 22 21.695984 21.907574 22 21.732051 C 22.304016 21.556527 22.556527 21.304016 22.732051 21 C 22.907574 20.695984 23 20.351047 23 20 L 23 15 C 22.99964 14.6493 22.907044 14.304788 22.731538 14.001163 C 22.556033 13.697538 22.303715 13.44535 22 13.27 Z";
}
