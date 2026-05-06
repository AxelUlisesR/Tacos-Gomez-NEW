using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Npgsql;

namespace Tacos_Gomez_NEW
{
    // CLASES MOVIDAS AFUERA PARA EVITAR ERRORES DE XAML
   

    public sealed partial class ConsultaVentas : Page
    {
        private string cadena = "Host=localhost;Port=5432;Database=Taqueria;Username=usuario;Password=1234;";
        public ObservableCollection<VentaItem> ListaVentas { get; set; } = new ObservableCollection<VentaItem>();
        public ObservableCollection<DetalleItem> ListaDetalle { get; set; } = new ObservableCollection<DetalleItem>();

        public ConsultaVentas()
        {
            this.InitializeComponent();
            lvVentas.ItemsSource = ListaVentas;
            lvDetalle.ItemsSource = ListaDetalle;
            CargarVentas();
        }

        private void VerificarEstadoBloqueo(string estado)
        {
            bool finalizada = (estado == "Pagada" || estado == "Cancelada");
            cbEstados.IsEnabled = !finalizada;
            btnActualizarEstado.IsEnabled = !finalizada;
            txtEstadoAviso.Visibility = finalizada ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void lvVentas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvVentas.SelectedItem is VentaItem v)
            {
                txtDetalleFolio.Text = v.Id.ToString();
                txtDetalleCliente.Text = v.Cliente;
                txtDetalleVendedor.Text = v.Vendedor;
                txtDetalleTotal.Text = v.TotalStr;
                txtDetalleFecha.Text = v.FechaStr; // Mostrar fecha en detalle[cite: 2]

                await CargarDetalle(v.Id);

                foreach (ComboBoxItem i in cbEstados.Items)
                    if (i.Content.ToString() == v.Estado) { cbEstados.SelectedItem = i; break; }

                VerificarEstadoBloqueo(v.Estado);
            }
        }

        private async void btnActualizarEstado_Click(object sender, RoutedEventArgs e)
        {
            if (lvVentas.SelectedItem is VentaItem v && cbEstados.SelectedItem is ComboBoxItem c)
            {
                string nombreEstadoNuevo = c.Content.ToString();
                string charEstado = nombreEstadoNuevo == "Activa" ? "R" : (nombreEstadoNuevo == "Pagada" ? "P" : "C");

                try
                {
                    using var conn = new NpgsqlConnection(cadena);
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand("UPDATE ordenes SET estado = @est WHERE idorden = @id", conn);
                    cmd.Parameters.AddWithValue("est", charEstado[0]);
                    cmd.Parameters.AddWithValue("id", v.Id);
                    await cmd.ExecuteNonQueryAsync();

                    v.Estado = nombreEstadoNuevo;
                    VerificarEstadoBloqueo(nombreEstadoNuevo);
                    CargarVentas(asbBusqueda.Text);
                }
                catch (Exception) { }
            }
        }

        private async void btnHistorico_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog confirmar = new ContentDialog
            {
                Title = "Mover a Históricos",
                Content = "¿Desea mover todas las órdenes PAGADAS y CANCELADAS a la base histórica?",
                PrimaryButtonText = "Mover",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };

            if (await confirmar.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                using var trans = await conn.BeginTransactionAsync();
                try
                {
                    // 1. Mover detalles
                    using var cmdDet = new NpgsqlCommand(@"INSERT INTO hist_detordenes 
                        SELECT d.* FROM detordenes d JOIN ordenes o ON d.idorden = o.idorden 
                        WHERE o.estado IN ('P', 'C')", conn);
                    await cmdDet.ExecuteNonQueryAsync();

                    // 2. Mover órdenes
                    using var cmdOrd = new NpgsqlCommand(@"INSERT INTO hist_ordenes 
                        SELECT * FROM ordenes WHERE estado IN ('P', 'C')", conn);
                    await cmdOrd.ExecuteNonQueryAsync();

                    // 3. Eliminar
                    using var cmdDelDet = new NpgsqlCommand(@"DELETE FROM detordenes WHERE idorden IN 
                        (SELECT idorden FROM ordenes WHERE estado IN ('P', 'C'))", conn);
                    await cmdDelDet.ExecuteNonQueryAsync();

                    using var cmdDelOrd = new NpgsqlCommand("DELETE FROM ordenes WHERE estado IN ('P', 'C')", conn);
                    await cmdDelOrd.ExecuteNonQueryAsync();

                    await trans.CommitAsync();
                    CargarVentas();
                    await MostrarMensaje("Éxito", "Archivado correctamente.");
                }
                catch { await trans.RollbackAsync(); throw; }
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private async void CargarVentas(string filtro = "")
        {
            try
            {
                ListaVentas.Clear();
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();
                // SQL incluyendo o.fecha[cite: 2]
                string sql = @"SELECT o.idorden, c.nombre, e.nombre, o.estado, o.total, o.fecha 
                               FROM ordenes o 
                               JOIN clientes c ON o.idcliente = c.idcliente
                               JOIN empleados e ON o.idempleado = e.idempleado";

                if (!string.IsNullOrWhiteSpace(filtro))
                {
                    if (int.TryParse(filtro, out _)) sql += " WHERE o.idorden = @f";
                    else sql += " WHERE c.nombre ILIKE @f";
                }
                sql += " ORDER BY o.idorden DESC";

                using var cmd = new NpgsqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(filtro))
                {
                    if (int.TryParse(filtro, out int f)) cmd.Parameters.AddWithValue("f", f);
                    else cmd.Parameters.AddWithValue("f", $"%{filtro}%");
                }

                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync())
                {
                    string est = dr.GetString(3);
                    ListaVentas.Add(new VentaItem
                    {
                        Id = dr.GetInt32(0),
                        Cliente = dr.GetString(1),
                        Vendedor = dr.GetString(2),
                        Estado = (est == "R" || est == "A") ? "Activa" : (est == "P" ? "Pagada" : "Cancelada"),
                        Total = dr.GetDouble(4),
                        Fecha = dr.GetDateTime(5) // Carga de fecha[cite: 2]
                    });
                }
            }
            catch { }
        }

        private async Task CargarDetalle(int id)
        {
            ListaDetalle.Clear();
            using var conn = new NpgsqlConnection(cadena);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"SELECT p.nombre, d.cantidad, d.subtotal 
                                               FROM detordenes d 
                                               JOIN productos p ON d.idproducto = p.idproducto 
                                               WHERE d.idorden = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
                ListaDetalle.Add(new DetalleItem { Producto = dr.GetString(0), Cantidad = dr.GetInt32(1), Subtotal = dr.GetDouble(2) });
        }

        private async Task MostrarMensaje(string t, string c)
        {
            await new ContentDialog { Title = t, Content = c, CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();
        }

        private async void asbBusqueda_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && !string.IsNullOrWhiteSpace(sender.Text))
            {
                try
                {
                    using var conn = new NpgsqlConnection(cadena);
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand("SELECT nombre FROM clientes WHERE nombre ILIKE @f LIMIT 5", conn);
                    cmd.Parameters.AddWithValue("f", $"%{sender.Text}%");
                    List<string> sugerencias = new List<string>();
                    using var dr = await cmd.ExecuteReaderAsync();
                    while (await dr.ReadAsync()) sugerencias.Add(dr.GetString(0));
                    sender.ItemsSource = sugerencias;
                }
                catch { }
            }
        }
        private void asbBusqueda_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) => sender.Text = args.SelectedItem.ToString();
        private void asbBusqueda_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => CargarVentas(sender.Text);
        private void btnRefrescar_Click(object sender, RoutedEventArgs e) { asbBusqueda.Text = ""; CargarVentas(); }
    }
}