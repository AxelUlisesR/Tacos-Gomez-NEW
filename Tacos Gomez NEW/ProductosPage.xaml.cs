using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Npgsql;

namespace Tacos_Gomez_NEW
{
    public class Producto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public double Precio { get; set; }
        public string Categoria { get; set; }
        public string PrecioFormateado => Precio.ToString("C2"); // Muestra $0.00
    }

    public sealed partial class ProductosPage : Page
    {
        private string cadena = "Host=localhost;Port=5432;Database=Taqueria;Username=usuario;Password=1234;";
        private ObservableCollection<Producto> ListaProductos = new ObservableCollection<Producto>();

        public ProductosPage()
        {
            this.InitializeComponent();
            lvProductos.ItemsSource = ListaProductos;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _ = CargarProductos();
            EstadoInicial();
        }

        // ========================= MÉTODOS DB =========================
        private async Task CargarProductos()
        {
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                ListaProductos.Clear();
                using var cmd = new NpgsqlCommand("SELECT idproducto, nombre, precio, categoria FROM productos ORDER BY idproducto", conn);
                using var lector = await cmd.ExecuteReaderAsync();
                while (await lector.ReadAsync())
                {
                    ListaProductos.Add(new Producto
                    {
                        Id = lector.GetInt32(0),
                        Nombre = lector.GetString(1),
                        Precio = lector.GetDouble(2),
                        Categoria = lector.IsDBNull(3) ? "" : lector.GetString(3)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private async void btnGrabar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombre.Text) || cbCategoria.SelectedItem == null) return;
            string cat = (cbCategoria.SelectedItem as ComboBoxItem).Content.ToString();

            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("INSERT INTO productos (nombre, precio, categoria) VALUES (@n, @p, @c)", conn);
                cmd.Parameters.AddWithValue("n", txtNombre.Text);
                cmd.Parameters.AddWithValue("p", nbPrecio.Value);
                cmd.Parameters.AddWithValue("c", cat);
                await cmd.ExecuteNonQueryAsync();

                await CargarProductos();
                EstadoInicial();
            }
            catch (Exception ex) { await MostrarDialogo("Error", ex.Message); }
        }

        private async void btnModificar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtId.Text)) return;
            string cat = (cbCategoria.SelectedItem as ComboBoxItem).Content.ToString();

            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("UPDATE productos SET nombre=@n, precio=@p, categoria=@c WHERE idproducto=@id", conn);
                cmd.Parameters.AddWithValue("n", txtNombre.Text);
                cmd.Parameters.AddWithValue("p", nbPrecio.Value);
                cmd.Parameters.AddWithValue("c", cat);
                cmd.Parameters.AddWithValue("id", Convert.ToInt32(txtId.Text));
                await cmd.ExecuteNonQueryAsync();

                await CargarProductos();
                EstadoInicial();
            }
            catch (Exception ex) { await MostrarDialogo("Error", ex.Message); }
        }

        private async void btnBusca_Click(object sender, RoutedEventArgs e)
        {
            TextBox input = new TextBox { Header = "ID Producto", PlaceholderText = "Escribe el ID..." };
            ContentDialog diag = new ContentDialog { Title = "Buscar Producto", Content = input, PrimaryButtonText = "Ir", CloseButtonText = "Cancelar", XamlRoot = this.XamlRoot };

            if (await diag.ShowAsync() == ContentDialogResult.Primary && int.TryParse(input.Text, out int id))
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT * FROM productos WHERE idproducto=@id", conn);
                cmd.Parameters.AddWithValue("id", id);
                using var lector = await cmd.ExecuteReaderAsync();
                if (await lector.ReadAsync())
                {
                    txtId.Text = lector["idproducto"].ToString();
                    txtNombre.Text = lector["nombre"].ToString();
                    nbPrecio.Value = Convert.ToDouble(lector["precio"]);
                    string cat = lector["categoria"].ToString();
                    foreach (ComboBoxItem item in cbCategoria.Items) if (item.Content.ToString() == cat) cbCategoria.SelectedItem = item;

                    ActivarCampos(true);
                    btnModificar.IsEnabled = true; btnNuevo.IsEnabled = btnGrabar.IsEnabled = false;
                }
                else await MostrarDialogo("Búsqueda", "ID de producto no encontrado.");
            }
        }

        // ========================= UTILIDADES =========================
        private void EstadoInicial()
        {
            ActivarCampos(false);
            btnNuevo.IsEnabled = btnBusca.IsEnabled = true;
            btnGrabar.IsEnabled = btnModificar.IsEnabled = false;
            LimpiarCampos();
        }

        private void LimpiarCampos()
        {
            txtId.Text = txtNombre.Text = "";
            nbPrecio.Value = 0;
            cbCategoria.SelectedIndex = -1;
        }

        private void ActivarCampos(bool estado)
        {
            txtNombre.IsEnabled = nbPrecio.IsEnabled = cbCategoria.IsEnabled = estado;
        }

        private async void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarCampos(); ActivarCampos(true);
            btnGrabar.IsEnabled = true; btnNuevo.IsEnabled = false;
            using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(idproducto), 0) + 1 FROM productos", conn);
            txtId.Text = (await cmd.ExecuteScalarAsync()).ToString();
        }

        private void lvProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvProductos.SelectedItem is Producto p)
            {
                txtId.Text = p.Id.ToString();
                txtNombre.Text = p.Nombre;
                nbPrecio.Value = p.Precio;
                foreach (ComboBoxItem item in cbCategoria.Items) if (item.Content.ToString() == p.Categoria) cbCategoria.SelectedItem = item;

                ActivarCampos(true);
                btnModificar.IsEnabled = true; btnGrabar.IsEnabled = false;
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) => EstadoInicial();

        private async Task MostrarDialogo(string t, string c)
        {
            ContentDialog d = new ContentDialog { Title = t, Content = c, CloseButtonText = "OK", XamlRoot = this.XamlRoot };
            await d.ShowAsync();
        }
    }
}