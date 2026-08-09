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

    protected override DataTemplate SelectTemplateCore(object item)
    {
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
                _ => base.SelectTemplateCore(item)
            };
        }
        return base.SelectTemplateCore(item);
    }
}
