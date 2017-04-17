using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace CNCController.Wpf.Views
{
    [ContentProperty(nameof(Content))]
    public class Milling2DControl : ContentControl
    {

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        public ICommand MoveCommand
        {
            get { return (ICommand)GetValue(MoveCommandProperty); }
            set { SetValue(MoveCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MoveCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MoveCommandProperty =
            DependencyProperty.Register("MoveCommand", typeof(ICommand), typeof(Milling2DControl), new PropertyMetadata(null));




        public ICommand LineCommand
        {
            get { return (ICommand)GetValue(LineCommandProperty); }
            set { SetValue(LineCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LineCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineCommandProperty =
            DependencyProperty.Register("LineCommand", typeof(ICommand), typeof(Milling2DControl), new PropertyMetadata(null));

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            var pos = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (LineCommand?.CanExecute(pos) ?? false)
                    LineCommand?.Execute(pos);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                if (MoveCommand?.CanExecute(pos) ?? false)
                    MoveCommand?.Execute(pos);
            }
        }
    }
}
