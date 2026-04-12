using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace Tacos_Gomez_NEW
{
    public sealed partial class BlankPage1 : Page
    {
        public BlankPage1()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Mostramos el rol del usuario que viene del Login
            txtRolActual.Text = $"Sesión iniciada como: {UsuarioSesion.Rol}";

            // Restricciones de seguridad por Rol
            if (UsuarioSesion.Rol != "Administrador")
            {
                itemEmpleados.IsEnabled = false;
                itemReportes.IsEnabled = false;

                // OCULTAR EL BOTÓN DE SETTINGS SI NO ES ADMIN
                NavMenu.IsSettingsVisible = false;
            }
        }

        private async void NavMenu_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            // 1. CHECAMOS PRIMERO SI SE PRESIONÓ EL BOTÓN DE SETTINGS
            if (args.IsSettingsInvoked)
            {
                WelcomePanel.Visibility = Visibility.Collapsed;
                ContentFrame.Navigate(typeof(SettingsPage)); // Navega a tu página de Backups
                return;
            }

            // 2. Lógica para el resto de los ítems con Tag
            if (args.InvokedItemContainer.Tag == null) return;

            var tag = args.InvokedItemContainer.Tag.ToString();

            // Ocultamos el mensaje de bienvenida al navegar
            if (tag != "logout" && tag != "salir")
            {
                WelcomePanel.Visibility = Visibility.Collapsed;
            }

            switch (tag)
            {
                case "productos":
                    ContentFrame.Navigate(typeof(ProductosPage));
                    break;

                case "empleados":
                    if (UsuarioSesion.Rol == "Administrador")
                        ContentFrame.Navigate(typeof(EmpleadosPage));
                    else
                        await MostrarError("Acceso denegado. Requiere permisos de Administrador.");
                    break;

                case "clientes":
                    ContentFrame.Navigate(typeof(ClientesPage));
                    break;

                case "ventas":
                    ContentFrame.Navigate(typeof(VentasPage));
                    break;

                case "consultas":
                    ContentFrame.Navigate(typeof(ConsultaVentas));
                    break;

                case "reportes":
                    if (UsuarioSesion.Rol == "Administrador")
                        ContentFrame.Navigate(typeof(GeneradorReportes));
                    else
                        await MostrarError("Acceso denegado a reportes.");
                    break;

                case "logout":
                    UsuarioSesion.Nombre = string.Empty;
                    UsuarioSesion.Rol = string.Empty;
                    if (this.Frame != null)
                    {
                        this.Frame.Navigate(typeof(LoginPage));
                    }
                    break;

                case "salir":
                    Application.Current.Exit();
                    break;
            }
        }

        private async Task MostrarError(string mensaje)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "Seguridad del Sistema",
                Content = mensaje,
                CloseButtonText = "Entendido",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}