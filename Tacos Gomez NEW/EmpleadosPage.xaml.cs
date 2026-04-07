using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Tacos_Gomez_NEW
{
    public class Empleado
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string Numero { get; set; }
        public string Telefono { get; set; }
        public string Rol { get; set; }
    }

    public sealed partial class EmpleadosPage : Page
    {
        private string cadena = "Host=localhost;Port=5432;Database=Taqueria;Username=usuario;Password=1234;";
        private ObservableCollection<Empleado> ListaEmpleados = new ObservableCollection<Empleado>();

        public EmpleadosPage()
        {
            this.InitializeComponent();
            lvEmpleados.ItemsSource = ListaEmpleados;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _ = CargarEmpleados();
            EstadoInicial();
        }

        // ========================= SEGURIDAD MD5 =========================
        private string CalcularMD5(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ========================= AUTOCOMPLETADO (INEGI) =========================
        private async void asbDireccion_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && sender.Text.Trim().Length >= 2)
            {
                try
                {
                    List<string> sugerencias = new List<string>();
                    using var conn = new NpgsqlConnection(cadena);
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                        "SELECT tipo_vialidad || ' ' || nombre FROM catalogo_vialidades WHERE nombre ILIKE @p ORDER BY nombre LIMIT 8", conn);
                    cmd.Parameters.AddWithValue("p", $"%{sender.Text}%");
                    using var lector = await cmd.ExecuteReaderAsync();
                    while (await lector.ReadAsync()) sugerencias.Add(lector.GetString(0));
                    sender.ItemsSource = sugerencias;
                }
                catch { }
            }
        }

        private void asbDireccion_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            sender.Text = args.SelectedItem.ToString(); // Solo la calle, el número va en el otro campo
        }

        // ========================= CRUD CON TRANSACCIONES =========================
        private async Task CargarEmpleados()
        {
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                ListaEmpleados.Clear();
                using var cmd = new NpgsqlCommand("SELECT idempleado, nombre, direccion, telefono, rol, numero FROM empleados ORDER BY idempleado", conn);
                using var lector = await cmd.ExecuteReaderAsync();
                while (await lector.ReadAsync())
                {
                    ListaEmpleados.Add(new Empleado
                    {
                        Id = lector.GetInt32(0),
                        Nombre = lector.GetString(1),
                        Direccion = lector.IsDBNull(2) ? "" : lector.GetString(2),
                        Telefono = lector.IsDBNull(3) ? "" : lector.GetString(3),
                        Rol = lector.IsDBNull(4) ? "" : lector.GetString(4),
                        Numero = lector.IsDBNull(5) ? "" : lector.GetString(5)
                    });
                }
            }
            catch { }
        }

        private async void btnGrabar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombre.Text) || cbRol.SelectedItem == null) return;
            string rol = (cbRol.SelectedItem as ComboBoxItem).Content.ToString();

            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                using var trans = await conn.BeginTransactionAsync();
                try
                {
                    int id;
                    using (var cmd = new NpgsqlCommand("INSERT INTO empleados (nombre, direccion, numero, telefono, rol) VALUES (@n, @d, @num, @t, @r) RETURNING idempleado", conn))
                    {
                        cmd.Parameters.AddWithValue("n", txtNombre.Text);
                        cmd.Parameters.AddWithValue("d", asbDireccion.Text);
                        cmd.Parameters.AddWithValue("num", txtNumero.Text);
                        cmd.Parameters.AddWithValue("t", txtTelefono.Text);
                        cmd.Parameters.AddWithValue("r", rol);
                        id = (int)await cmd.ExecuteScalarAsync();
                    }
                    using (var cmdU = new NpgsqlCommand("INSERT INTO usuarios_sistema (username, password_hash, id_empleado, rol) VALUES (@u, @p, @ide, @rol)", conn))
                    {
                        cmdU.Parameters.AddWithValue("u", txtUsername.Text);
                        cmdU.Parameters.AddWithValue("p", CalcularMD5(txtPassword.Password));
                        cmdU.Parameters.AddWithValue("ide", id);
                        cmdU.Parameters.AddWithValue("rol", rol);
                        await cmdU.ExecuteNonQueryAsync();
                    }
                    await trans.CommitAsync();
                    await CargarEmpleados(); EstadoInicial();
                }
                catch { await trans.RollbackAsync(); throw; }
            }
            catch (Exception ex) { await MostrarDialogo("Error", ex.Message); }
        }

        private async void btnModificar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtId.Text) || cbRol.SelectedItem == null) return;
            string rol = (cbRol.SelectedItem as ComboBoxItem).Content.ToString();
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("UPDATE empleados SET nombre=@n, direccion=@d, numero=@num, telefono=@t, rol=@r WHERE idempleado=@id", conn);
                cmd.Parameters.AddWithValue("n", txtNombre.Text);
                cmd.Parameters.AddWithValue("d", asbDireccion.Text);
                cmd.Parameters.AddWithValue("num", txtNumero.Text);
                cmd.Parameters.AddWithValue("t", txtTelefono.Text);
                cmd.Parameters.AddWithValue("r", rol);
                cmd.Parameters.AddWithValue("id", Convert.ToInt32(txtId.Text));
                await cmd.ExecuteNonQueryAsync();

                using var cmdU = new NpgsqlCommand("UPDATE usuarios_sistema SET rol=@r WHERE id_empleado=@id", conn);
                cmdU.Parameters.AddWithValue("r", rol);
                cmdU.Parameters.AddWithValue("id", Convert.ToInt32(txtId.Text));
                await cmdU.ExecuteNonQueryAsync();

                await CargarEmpleados(); EstadoInicial();
            }
            catch (Exception ex) { await MostrarDialogo("Error", ex.Message); }
        }

        // ========================= ESTADOS Y UTILIDADES =========================
        private void EstadoInicial()
        {
            txtNombre.IsEnabled = asbDireccion.IsEnabled = txtNumero.IsEnabled = txtTelefono.IsEnabled = cbRol.IsEnabled =
            txtUsername.IsEnabled = txtPassword.IsEnabled = false;
            btnNuevo.IsEnabled = true; btnGrabar.IsEnabled = btnModificar.IsEnabled = false;
            LimpiarCampos();
        }

        private void LimpiarCampos()
        {
            txtId.Text = txtNombre.Text = asbDireccion.Text = txtNumero.Text = txtTelefono.Text = txtUsername.Text = txtPassword.Password = "";
            cbRol.SelectedIndex = -1;
        }

        private async void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos();
            txtNombre.IsEnabled = asbDireccion.IsEnabled = txtNumero.IsEnabled = txtTelefono.IsEnabled = cbRol.IsEnabled =
            txtUsername.IsEnabled = txtPassword.IsEnabled = true;
            btnGrabar.IsEnabled = true; btnNuevo.IsEnabled = false;
            using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(idempleado), 0) + 1 FROM empleados", conn);
            txtId.Text = (await cmd.ExecuteScalarAsync()).ToString();
        }

        private void lvEmpleados_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvEmpleados.SelectedItem is Empleado s)
            {
                txtId.Text = s.Id.ToString(); txtNombre.Text = s.Nombre; asbDireccion.Text = s.Direccion;
                txtNumero.Text = s.Numero; txtTelefono.Text = s.Telefono;
                foreach (ComboBoxItem item in cbRol.Items) if (item.Content.ToString() == s.Rol) cbRol.SelectedItem = item;
                txtNombre.IsEnabled = asbDireccion.IsEnabled = txtNumero.IsEnabled = txtTelefono.IsEnabled = cbRol.IsEnabled = true;
                txtUsername.IsEnabled = txtPassword.IsEnabled = false;
                btnModificar.IsEnabled = true; btnGrabar.IsEnabled = false;
            }
        }

        private async void btnBusca_Click(object sender, RoutedEventArgs e)
        {
            TextBox input = new TextBox { Header = "ID Empleado" };
            ContentDialog diag = new ContentDialog { Title = "Buscar", Content = input, PrimaryButtonText = "Ir", CloseButtonText = "Cancelar", XamlRoot = this.XamlRoot };
            if (await diag.ShowAsync() == ContentDialogResult.Primary && int.TryParse(input.Text, out int id))
            {
                using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT * FROM empleados WHERE idempleado=@id", conn);
                cmd.Parameters.AddWithValue("id", id);
                using var lector = await cmd.ExecuteReaderAsync();
                if (await lector.ReadAsync())
                {
                    txtId.Text = lector["idempleado"].ToString();
                    txtNombre.Text = lector["nombre"].ToString();
                    asbDireccion.Text = lector["direccion"].ToString();
                    txtNumero.Text = lector["numero"].ToString();
                    txtTelefono.Text = lector["telefono"].ToString();
                    string rol = lector["rol"].ToString();
                    foreach (ComboBoxItem item in cbRol.Items) if (item.Content.ToString() == rol) cbRol.SelectedItem = item;
                    txtNombre.IsEnabled = asbDireccion.IsEnabled = txtNumero.IsEnabled = txtTelefono.IsEnabled = cbRol.IsEnabled = true;
                    btnModificar.IsEnabled = true; btnNuevo.IsEnabled = btnGrabar.IsEnabled = false;
                }
                else await MostrarDialogo("Búsqueda", "ID no encontrado.");
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) => EstadoInicial();

        private async Task MostrarDialogo(string t, string c)
        {
            ContentDialog d = new ContentDialog { Title = t, Content = c, CloseButtonText = "Aceptar", XamlRoot = this.XamlRoot };
            await d.ShowAsync();
        }
    }
}