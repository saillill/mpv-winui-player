using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace mpv_winui.Modules.Settings.Controls;

public partial class OptionTemplateSelector : DataTemplateSelector
{
    public DataTemplate SectionHeaderTemplate { get; set; } = null!;
    public DataTemplate BooleanTemplate { get; set; } = null!;
    public DataTemplate TextTemplate { get; set; } = null!;
    public DataTemplate IntegerTemplate { get; set; } = null!;
    public DataTemplate DoubleTemplate { get; set; } = null!;
    public DataTemplate TextListTemplate { get; set; } = null!;
    public DataTemplate ColorTemplate { get; set; } = null!;
    public DataTemplate ActionTemplate { get; set; } = null!;
    public DataTemplate CheckListTemplate { get; set; } = null!;
    public DataTemplate LayoutTemplate { get; set; } = null!;
    public DataTemplate MultiListTemplate { get; set; } = null!;
    public DataTemplate ShaderListTemplate { get; set; } = null!;

    /// <summary>
    /// While the inline customize mode is on, every row renders through the
    /// edit templates instead of its value control, so the mode covers sections
    /// (2nd level) and options alike with one selector.
    /// </summary>
    public bool CustomizeMode { get; set; }

    public DataTemplate? CustomizeOptionTemplate { get; set; }
    public DataTemplate? CustomizeSectionTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        if (CustomizeMode)
        {
            if (item is SectionHeaderItem && CustomizeSectionTemplate is not null)
            {
                return CustomizeSectionTemplate;
            }
            if (item is Option && CustomizeOptionTemplate is not null)
            {
                return CustomizeOptionTemplate;
            }
        }

        if (item is SectionHeaderItem)
        {
            return SectionHeaderTemplate;
        }
        if (item is Option option)
        {
            return option.Type switch
            {
                OptionType.Boolean => BooleanTemplate,
                OptionType.String => TextTemplate,
                OptionType.Integer => IntegerTemplate,
                OptionType.Double => DoubleTemplate,
                OptionType.StringList => TextListTemplate,
                OptionType.Color => ColorTemplate,
                OptionType.Action => ActionTemplate,
                OptionType.CheckList => CheckListTemplate,
                OptionType.Layout => LayoutTemplate,
                OptionType.MultiList => MultiListTemplate,
                OptionType.ShaderList => ShaderListTemplate,
                _ => base.SelectTemplateCore(item)
            };
        }
        return base.SelectTemplateCore(item);
    }
}
