using Microsoft.UI.Xaml.Controls;
using System;

namespace mpv_winui.Infrastructure.Controls
{
    public partial class CheckBoxExt : CheckBox
    {
        public event EventHandler<bool>? CheckChanged;

        public CheckBoxExt()
        {
            DefaultStyleKey = typeof(CheckBoxExt);
        }

        protected override void OnToggle()
        {
            base.OnToggle();
            CheckChanged?.Invoke(this, IsChecked == true);
        }
    }
}
