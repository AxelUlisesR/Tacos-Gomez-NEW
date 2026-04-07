using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

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
            txtRolActual.Text = $"Sesión iniciada como: {UsuarioSesion.Rol}";

            if (UsuarioSesion.Rol == "Empleado")
            {
                // Deshabilitamos o escondemos opciones según tus permisos de WinForms
                itemEmpleados.IsEnabled = false;
                itemReportes.IsEnabled = false;

                // Opcional: Puedes ocultarlos completamente si prefieres
                // itemEmpleados.Visibility = Visibility.Collapsed;
            }
        }

        private async void NavMenu_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var tag = args.InvokedItemContainer.Tag.ToString();
            WelcomePanel.Visibility = Visibility.Collapsed;

            switch (tag)
            {
                case "productos":
                     ContentFrame.Navigate(typeof(ProductosPage)); 
                    break;

                case "empleados":
                    if (UsuarioSesion.Rol == "Administrador")
                    {
                         ContentFrame.Navigate(typeof(EmpleadosPage));
                    }
                    else
                    {
                        await MostrarError("Acceso denegado. Solo administradores.");
                    }
                    break;

                case "clientes":
                    ContentFrame.Navigate(typeof(ClientesPage));
                    break;

                case "ventas":
                     ContentFrame.Navigate(typeof(VentasPage));
                    break;

                case "reportes":
                    if (UsuarioSesion.Rol == "Administrador")
                    {
                        // ContentFrame.Navigate(typeof(ReportesPage));
                    }
                    else
                    {
                        await MostrarError("Acceso denegado.");
                    }
                    break;

                case "salir":
                    Application.Current.Exit();
                    break;
            }
        }

        private async System.Threading.Tasks.Task MostrarError(string mensaje)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "Seguridad",
                Content = mensaje,
                CloseButtonText = "Entendido",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}