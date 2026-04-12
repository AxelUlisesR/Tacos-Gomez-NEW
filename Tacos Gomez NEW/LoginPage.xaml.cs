using System;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Npgsql;

namespace Tacos_Gomez_NEW
{
    public static class UsuarioSesion
    {
        public static string Rol { get; set; }
        public static string Nombre { get; set; }
    }

    public sealed partial class LoginPage : Page
    {
        // Tu cadena de conexión a PostgreSQL
        private readonly string cadena = "Host=localhost;Port=5432;Database=Taqueria;Username=usuario;Password=1234;";

        public LoginPage()
        {
            this.InitializeComponent();
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            // Validar campos vacíos
            if (string.IsNullOrWhiteSpace(txtUsuario.Text) || string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                await MostrarMensaje("Campos requeridos", "Por favor, ingresa tu usuario y contraseña.");
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();

                // Consulta para validar credenciales
                using var cmd = new NpgsqlCommand("SELECT rol FROM usuarios_sistema WHERE username = @u AND password_hash = @p", conn);
                cmd.Parameters.AddWithValue("@u", txtUsuario.Text);
                cmd.Parameters.AddWithValue("@p", CalcularMD5(txtPassword.Password));

                var res = await cmd.ExecuteScalarAsync();

                if (res != null)
                {
                    // Guardar datos de sesión
                    UsuarioSesion.Rol = res.ToString();
                    UsuarioSesion.Nombre = txtUsuario.Text;

                    // Navegar al menú principal (BlankPage1)
                    this.Frame.Navigate(typeof(BlankPage1));
                }
                else
                {
                    // Si no encuentra el usuario o la contraseña es incorrecta
                    await MostrarMensaje("Acceso Fallido", "El usuario o la contraseña son incorrectos.");
                    txtPassword.Password = ""; // Limpiar campo de contraseña
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores de base de datos o red
                await MostrarMensaje("Error Crítico", "No se pudo conectar con el servidor: " + ex.Message);
            }
        }

        // Método para mostrar mensajes en WinUI 3 (ContentDialog)
        private async Task MostrarMensaje(string titulo, string contenido)
        {
            ContentDialog dialogo = new ContentDialog
            {
                Title = titulo,
                Content = contenido,
                CloseButtonText = "Aceptar",
                XamlRoot = this.XamlRoot // Crucial en WinUI 3
            };

            await dialogo.ShowAsync();
        }

        // Método para encriptar la contraseña en MD5
        private string CalcularMD5(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}