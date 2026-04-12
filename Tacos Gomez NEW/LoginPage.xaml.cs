using System;
using System.Text;
using System.Security.Cryptography;
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
        private readonly string cadena = "Host=localhost;Port=5432;Database=Taqueria;Username=usuario;Password=1234;";

        public LoginPage() { this.InitializeComponent(); }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsuario.Text) || string.IsNullOrWhiteSpace(txtPassword.Password)) return;

            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT rol FROM usuarios_sistema WHERE username = @u AND password_hash = @p", conn);
                cmd.Parameters.AddWithValue("@u", txtUsuario.Text);
                cmd.Parameters.AddWithValue("@p", CalcularMD5(txtPassword.Password));

                var res = await cmd.ExecuteScalarAsync();
                if (res != null)
                {
                    UsuarioSesion.Rol = res.ToString();
                    UsuarioSesion.Nombre = txtUsuario.Text;

                    // NAVEGAR AL MENÚ (BlankPage1)
                    this.Frame.Navigate(typeof(BlankPage1));
                }
                else { /* Mostrar error de credenciales */ }
            }
            catch (Exception ex) { /* Manejar error */ }
        }

        private string CalcularMD5(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}