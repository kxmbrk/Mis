using System;
using System.Windows;

namespace Playground.WpfApp.Behaviors
{
    public class WindowCloser
    {
        public static bool GetEnableWindowClosing(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableWindowClosingProperty);
        }

        public static void SetEnableWindowClosing(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableWindowClosingProperty, value);
        }

        // Using a DependencyProperty as the backing store for EnableWindowClosing.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty EnableWindowClosingProperty =
            DependencyProperty.RegisterAttached("EnableWindowClosing", typeof(bool), typeof(WindowCloser), new PropertyMetadata(false, OnEnableWindowClosingChanged));

        private static void OnEnableWindowClosingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window)
            {
                window.Loaded += (o, ev) =>
                {
                    if (window.DataContext is ICloseWindow vm)
                    {
                        vm.Close += () =>
                        {
                            window.Close();
                        };

                        window.Closing += (obj, eventArg) =>
                        {
                            var canClose = vm.CanClose();
                            if (canClose)
                            {
                                vm.DisposeResources();
                            }
                            eventArg.Cancel = !canClose;
                        };
                    }
                };
            }
        }
    }

    public interface ICloseWindow
    {
        Action Close { get; set; }
        bool CanClose();

        void DisposeResources();
    }
}
