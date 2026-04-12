using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Tacos_Gomez_NEW
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Truco si el x:Name falla: buscamos el control manualmente
            var frame = this.Content as Frame;
            if (frame != null)
            {
                frame.Navigate(typeof(LoginPage));
            }
            else
            {
                // Si el frame no es el contenido directo, RootFrame debería funcionar tras compilar
                this.RootFrame.Navigate(typeof(LoginPage));
            }

            // Maximizar ventana
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                ((Microsoft.UI.Windowing.OverlappedPresenter)appWindow.Presenter).Maximize();
            }
        }
    }
}