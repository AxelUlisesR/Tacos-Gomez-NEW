using System;
using System.Text;
using System.Security.Cryptography;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Npgsql; 

namespace Tacos_Gomez_NEW
{
    public sealed partial class MainWindow : Window
    {
        private readonly NpgsqlConnection conexion = new NpgsqlConnection("Host=localhost;Port=5432;Database=Taqueria;Username=usuario;Password=1234;");

        public MainWindow()
        {
            this.InitializeComponent();
            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);

            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                ((Microsoft.UI.Windowing.OverlappedPresenter)appWindow.Presenter).Maximize();
            }
        }

        private string CalcularMD5(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsuario.Text) || string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                await MostrarDialogo("Error", "Por favor, llene todos los campos.");
                return;
            }

            try
            {
                await conexion.OpenAsync();
                string passCifrada = CalcularMD5(txtPassword.Password);

                using (var comando = new NpgsqlCommand())
                {
                    comando.Connection = conexion;
                    comando.CommandText = "SELECT rol FROM usuarios_sistema WHERE username = @user AND password_hash = @pass";
                    comando.Parameters.AddWithValue("@user", txtUsuario.Text);
                    comando.Parameters.AddWithValue("@pass", passCifrada);

                    object resultado = await comando.ExecuteScalarAsync();

                    if (resultado != null)
                    {
                        UsuarioSesion.Rol = resultado.ToString();
                        UsuarioSesion.Nombre = txtUsuario.Text;

                        await MostrarDialogo("Bienvenido", $"Acceso concedido como: {UsuarioSesion.Rol}");
                        this.RootFrame.Navigate(typeof(BlankPage1));
                        
                    }
                    else
                    {
                        await MostrarDialogo("Acceso Denegado", "Usuario o contraseña incorrectos.");
                    }
                }
            }
            catch (Exception ex)
            {
                await MostrarDialogo("Error de Conexión", ex.Message);
            }
            finally
            {
                await conexion.CloseAsync();
            }
        }
        private async System.Threading.Tasks.Task MostrarDialogo(string titulo, string contenido)
        {
            ContentDialog dialogo = new ContentDialog
            {
                Title = titulo,
                Content = contenido,
                CloseButtonText = "Aceptar",
                XamlRoot = this.Content.XamlRoot 
            };
            await dialogo.ShowAsync();
        }
    }

    public static class UsuarioSesion
    {
        public static string Rol { get; set; }
        public static string Nombre { get; set; }
    }
}