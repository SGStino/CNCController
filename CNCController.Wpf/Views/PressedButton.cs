using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CNCController.Wpf.Views
{
    public class PressedButton : Button
    {
        public PressedButton()
        {
        }

        protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnIsPressedChanged(e);
            IsButtonPressed = (bool)e.NewValue;
        }



        public bool IsButtonPressed
        {
            get { return (bool)GetValue(IsButtonPressedProperty); }
            set { SetValue(IsButtonPressedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsButtonPressed.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsButtonPressedProperty =
            DependencyProperty.Register("IsButtonPressed", typeof(bool), typeof(PressedButton), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


    }
}
