using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.System;
using WinRT.Interop;
using System.Runtime.InteropServices;

namespace Tacos_Gomez_NEW
{
    public sealed partial class BlankPage1 : Page
    {
        // Importación de funciones de Windows para forzar el primer plano
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public BlankPage1()
        {
            this.InitializeComponent();

            // Detectar tecla F1 para ayuda contextual
            this.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.F1)
                {
                    AbrirVentanaAyuda();
                }
            };
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            txtRolActual.Text = $"Sesión iniciada como: {UsuarioSesion.Rol}";
            if (UsuarioSesion.Rol != "Administrador")
            {
                itemEmpleados.IsEnabled = false;
                itemProductos.IsEnabled = false;
                itemReportes.IsEnabled = false;
                NavMenu.IsSettingsVisible = false;
            }
        }

        private void NavMenu_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                WelcomePanel.Visibility = Visibility.Collapsed;
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            var item = args.InvokedItemContainer;
            if (item == null || item.Tag == null) return;

            string tag = item.Tag.ToString();

            // Si no es ayuda, cambiamos el contenido interno
            if (tag != "ayuda") WelcomePanel.Visibility = Visibility.Collapsed;

            switch (tag)
            {
                case "productos": ContentFrame.Navigate(typeof(ProductosPage)); break;
                case "empleados": ContentFrame.Navigate(typeof(EmpleadosPage)); break;
                case "clientes": ContentFrame.Navigate(typeof(ClientesPage)); break;
                case "ventas": ContentFrame.Navigate(typeof(VentasPage)); break;
                case "consultas": ContentFrame.Navigate(typeof(ConsultaVentas)); break;
                case "reportes":
                    if (UsuarioSesion.Rol == "Administrador") ContentFrame.Navigate(typeof(GeneradorReportes));
                    break;
                case "ayuda":
                    ContentFrame.Navigate(typeof(AyudaPage)); break;
                    break;
                case "logout":
                    CerrarSesion();
                    break;
                case "salir":
                    Application.Current.Exit();
                    break;
            }
        }

        private void AbrirVentanaAyuda()
        {
            // Creamos la nueva ventana
            Window ventanaAyuda = new Window();
            ventanaAyuda.Title = "Ayuda del Sistema - Tacos Gómez";

            // Configuramos el contenido
            Frame frameAyuda = new Frame();
            frameAyuda.Navigate(typeof(AyudaPage));
            ventanaAyuda.Content = frameAyuda;

            // La activamos
            ventanaAyuda.Activate();

            // FORZAR PRIMER PLANO: Obtenemos el ID de ventana y la empujamos al frente
            IntPtr handle = WindowNative.GetWindowHandle(ventanaAyuda);
            SetForegroundWindow(handle);
        }

        private void CerrarSesion()
        {
            UsuarioSesion.Nombre = string.Empty;
            UsuarioSesion.Rol = string.Empty;
            this.Frame?.Navigate(typeof(LoginPage));
        }
    }
}