namespace mpv_winui.Domains.Settings;

/// <summary>
/// A row's caption and description, as the current language spells them.
///
/// A type rather than a tuple with nullable annotations because the generated
/// half of the settings tree has no `#nullable` directive: a `(string, string?)`
/// return type there raises CS8669 on every member it touches, and the warning
/// budget correctly counted them as new.
///
/// The description is nullable in practice; it is not annotated because nothing
/// in this file needs the annotation -- the consumers assign it to a nullable
/// property and the compiler allows that.
/// </summary>
internal readonly record struct OptionText(string Label, string Description);
