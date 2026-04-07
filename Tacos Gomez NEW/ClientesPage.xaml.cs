using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Npgsql;

namespace Tacos_Gomez_NEW
{
    public class Cliente
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; } // Calle
        public string Numero { get; set; }    // Número exterior
        public string Telefono { get; set; }
    }

    public sealed partial class ClientesPage : Page
    {
        private string cadena = "Host=localhost;Port=5432;Database=Taqueria;Username=usuario;Password=1234;";
        private ObservableCollection<Cliente> ListaClientes = new ObservableCollection<Cliente>();

        public ClientesPage()
        {
            this.InitializeComponent();
            lvClientes.ItemsSource = ListaClientes;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _ = CargarClientes();
            EstadoInicial();
        }

        // ========================= AUTOCOMPLETADO (SAYULA) =========================
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
            sender.Text = args.SelectedItem.ToString();
        }

        // ========================= MÉTODOS DE BASE DE DATOS =========================
        private async Task CargarClientes()
        {
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                ListaClientes.Clear();
                using var cmd = new NpgsqlCommand("SELECT idcliente, nombre, direccion, numero, telefono FROM clientes ORDER BY idcliente", conn);
                using var lector = await cmd.ExecuteReaderAsync();
                while (await lector.ReadAsync())
                {
                    ListaClientes.Add(new Cliente
                    {
                        Id = lector.GetInt32(0),
                        Nombre = lector.IsDBNull(1) ? "" : lector.GetString(1),
                        Direccion = lector.IsDBNull(2) ? "" : lector.GetString(2),
                        Numero = lector.IsDBNull(3) ? "" : lector.GetString(3),
                        Telefono = lector.IsDBNull(4) ? "" : lector.GetString(4)
                    });
                }
            }
            catch { }
        }

        private async void btnGrabar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombre.Text)) return;
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("INSERT INTO clientes (nombre, direccion, numero, telefono) VALUES (@n, @d, @num, @t)", conn);
                cmd.Parameters.AddWithValue("n", txtNombre.Text);
                cmd.Parameters.AddWithValue("d", asbDireccion.Text);
                cmd.Parameters.AddWithValue("num", txtNumero.Text);
                cmd.Parameters.AddWithValue("t", txtTelefono.Text);
                await cmd.ExecuteNonQueryAsync();

                await CargarClientes();
                EstadoInicial();
            }
            catch (Exception ex) { await MostrarDialogo("Error", ex.Message); }
        }

        private async void btnModificar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtId.Text)) return;
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("UPDATE clientes SET nombre=@n, direccion=@d, numero=@num, telefono=@t WHERE idcliente=@id", conn);
                cmd.Parameters.AddWithValue("n", txtNombre.Text);
                cmd.Parameters.AddWithValue("d", asbDireccion.Text);
                cmd.Parameters.AddWithValue("num", txtNumero.Text);
                cmd.Parameters.AddWithValue("t", txtTelefono.Text);
                cmd.Parameters.AddWithValue("id", Convert.ToInt32(txtId.Text));
                await cmd.ExecuteNonQueryAsync();

                await CargarClientes();
                EstadoInicial();
            }
            catch (Exception ex) { await MostrarDialogo("Error", ex.Message); }
        }

        private async void btnBusca_Click(object sender, RoutedEventArgs e)
        {
            TextBox input = new TextBox { Header = "ID Cliente" };
            ContentDialog diag = new ContentDialog { Title = "Buscar Cliente", Content = input, PrimaryButtonText = "Ir", CloseButtonText = "Cancelar", XamlRoot = this.XamlRoot };

            if (await diag.ShowAsync() == ContentDialogResult.Primary && int.TryParse(input.Text, out int id))
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT * FROM clientes WHERE idcliente=@id", conn);
                cmd.Parameters.AddWithValue("id", id);
                using var lector = await cmd.ExecuteReaderAsync();
                if (await lector.ReadAsync())
                {
                    txtId.Text = lector["idcliente"].ToString();
                    txtNombre.Text = lector["nombre"].ToString();
                    asbDireccion.Text = lector["direccion"].ToString();
                    txtNumero.Text = lector["numero"].ToString();
                    txtTelefono.Text = lector["telefono"].ToString();
                    ActivarCampos(true);
                    btnModificar.IsEnabled = true; btnNuevo.IsEnabled = btnGrabar.IsEnabled = false;
                }
                else await MostrarDialogo("Búsqueda", "ID no encontrado.");
            }
        }

        // ========================= ESTADOS Y UTILIDADES =========================
        private void EstadoInicial()
        {
            ActivarCampos(false);
            btnNuevo.IsEnabled = true;
            btnGrabar.IsEnabled = btnModificar.IsEnabled = false;
            LimpiarCampos();
        }

        private void LimpiarCampos()
        {
            txtId.Text = txtNombre.Text = asbDireccion.Text = txtNumero.Text = txtTelefono.Text = "";
        }

        private void ActivarCampos(bool estado)
        {
            txtNombre.IsEnabled = asbDireccion.IsEnabled = txtNumero.IsEnabled = txtTelefono.IsEnabled = estado;
        }

        private async void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos(); ActivarCampos(true);
            btnGrabar.IsEnabled = true; btnNuevo.IsEnabled = false;
            using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(idcliente), 0) + 1 FROM clientes", conn);
            txtId.Text = (await cmd.ExecuteScalarAsync()).ToString();
        }

        private void lvClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvClientes.SelectedItem is Cliente s)
            {
                txtId.Text = s.Id.ToString(); txtNombre.Text = s.Nombre;
                asbDireccion.Text = s.Direccion; txtNumero.Text = s.Numero; txtTelefono.Text = s.Telefono;
                ActivarCampos(true);
                btnModificar.IsEnabled = true; btnGrabar.IsEnabled = false;
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