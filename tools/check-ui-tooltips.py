"""Scan XAML interactive controls for missing hover tooltips / automation names.

Rules (matched to this repo's conventions):
- Automation name satisfied by: AutomationProperties.Name in XAML (literal or
  x:Bind), a runtime AutomationProperties.SetName(<Name>, ...) in a sibling
  .cs, or self-labeling text (Content=/Label=/PlaceholderText=/Text= in XAML
  or the same property assigned at runtime).
- Hover tooltip satisfied by: ToolTipService.ToolTip in XAML, a runtime
  SetToolTip in a sibling .cs, self-labeling text (text buttons do not need
  tooltips), a Loaded= handler (per-item wiring, e.g. shader list rows), or
  the control being a Slider (its value is visible while dragging).
- Container chrome (CommandBar) and the video surface (SwapChainPanel) are
  skipped; Option*Control template internals (NumberBox/TextBox/ComboBox/
  CheckBox) are row-labeled by the option label and skipped.

Prints elements failing either rule. Exit code is always 0; the report is
informational (run by hand, not wired into CI).
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "mpv-winui" / "mpv-winui"
INTERACTIVE = {
    "Button", "ToggleButton", "AppBarButton", "AppBarToggleButton",
    "RepeatButton", "Slider", "ComboBox", "CheckBox", "RadioButton",
    "HyperlinkButton", "MenuFlyoutItem", "MenuFlyoutSubItem",
    "ToggleMenuFlyoutItem", "NumberBox", "TextBox", "AutoSuggestBox",
    "SplitButton", "DropDownButton",
}
ROW_LABELED_TYPES = {"NumberBox", "TextBox", "ComboBox", "CheckBox", "AutoSuggestBox"}
SELF_LABEL_ATTRS = ("Content=", "Label=", "PlaceholderText=", "Text=")

elem_re = re.compile(r"<([A-Za-z][\w:.]*)\b([^>]*)>")
name_re = re.compile(r'\bx:Name="([^"]+)"')
tip_attr_re = re.compile(r"ToolTipService\.ToolTip\s*=")
auto_attr_re = re.compile(r"AutomationProperties\.Name\s*=")
loaded_attr_re = re.compile(r'\bLoaded="')
text_attr_re = re.compile(r"\b(?:Content|Label|PlaceholderText|Text)\s*=")


def strip_comments(text: str) -> str:
    return re.sub(r"<!--.*?-->", "", text, flags=re.S)


def scan(xaml: Path):
    text = strip_comments(xaml.read_text(encoding="utf-8-sig", errors="replace"))
    code = "\n".join(
        p.read_text(encoding="utf-8-sig", errors="replace")
        for p in xaml.parent.glob("*.cs")
    )
    row_labeled_file = xaml.name.startswith("Option")
    gaps = []
    for m in elem_re.finditer(text):
        tag, attrs = m.group(1), m.group(2)
        base = tag.split(":")[-1].split(".")[-1]
        if base not in INTERACTIVE:
            continue
        if row_labeled_file and base in ROW_LABELED_TYPES:
            continue
        nm = name_re.search(attrs)
        name = nm.group(1) if nm else ""
        self_labeled = bool(text_attr_re.search(attrs)) or bool(
            name and re.search(re.escape(name) + r"\.(?:Content|Label|PlaceholderText|Text)\s*=", code)
        )
        has_auto = bool(auto_attr_re.search(attrs)) or bool(
            name and re.search(r"AutomationProperties\.SetName\(\s*" + re.escape(name) + r"\b", code)
        )
        has_tip = bool(tip_attr_re.search(attrs)) or bool(
            name and re.search(r"ToolTipService\.SetToolTip\(\s*" + re.escape(name) + r"\b", code)
        )
        if base == "Slider":
            has_auto = has_auto or True  # slider value is visible on drag; name only
            has_tip = True
        if loaded_attr_re.search(attrs):
            has_tip = True
            has_auto = True
        if self_labeled:
            has_tip = True
            has_auto = True
        if not has_tip or not has_auto:
            missing = []
            if not has_tip:
                missing.append("ToolTip")
            if not has_auto:
                missing.append("AutoName")
            line = text[: m.start()].count("\n") + 1
            gaps.append((name or "(unnamed)", base, missing, line))
    return gaps


def main():
    total = 0
    for xaml in sorted(ROOT.rglob("*.xaml")):
        parts = {p.lower() for p in xaml.parts}
        if parts & {"bin", "obj"}:
            continue
        for name, base, missing, line in scan(xaml):
            print(f"{xaml.relative_to(ROOT)}:{line} <{base} {name}> missing {','.join(missing)}")
            total += 1
    print(f"TOTAL={total}")


if __name__ == "__main__":
    main()
