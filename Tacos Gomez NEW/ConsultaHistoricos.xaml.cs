using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Npgsql;

namespace Tacos_Gomez_NEW
{
    // Clases movidas al nivel del namespace para que XAML las reconozca sin errores
    public class VentaHistoricaItem
    {
        public int Id { get; set; }
        public string Cliente { get; set; }
        public string Vendedor { get; set; }
        public string Estado { get; set; }
        public double Total { get; set; }
        public DateTime Fecha { get; set; }

        public string TotalStr => Total.ToString("C2");
        // Formato de 12 horas con am/pm
        public string FechaStr => Fecha.ToString("dd/MM/yyyy hh:mm tt");
    }

    public sealed partial class ConsultaHistoricos : Page
    {
        private string cadena = "Host=localhost;Port=5432;Database=Taqueria;Username=usuario;Password=1234;";
        public ObservableCollection<VentaHistoricaItem> ListaHistoricos { get; set; } = new ObservableCollection<VentaHistoricaItem>();
        public ObservableCollection<DetalleItem> ListaDetalle { get; set; } = new ObservableCollection<DetalleItem>();

        public ConsultaHistoricos()
        {
            this.InitializeComponent();
            lvVentas.ItemsSource = ListaHistoricos;
            lvDetalle.ItemsSource = ListaDetalle;
            CargarHistoricos();
        }

        private async void lvVentas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvVentas.SelectedItem is VentaHistoricaItem v)
            {
                txtDetalleFolio.Text = v.Id.ToString();
                txtDetalleCliente.Text = v.Cliente;
                txtEstadoFinal.Text = v.Estado;
                txtDetalleTotal.Text = v.TotalStr;
                txtDetalleFecha.Text = v.FechaStr; // Se asigna la fecha al detalle
                await CargarDetalle(v.Id);
            }
        }

        private async void CargarHistoricos(string filtro = "")
        {
            try
            {
                ListaHistoricos.Clear();
                using var conn = new NpgsqlConnection(cadena);
                await conn.OpenAsync();

                // SQL actualizado con columna o.fecha
                string sql = @"SELECT o.idorden, c.nombre, e.nombre, o.estado, o.total, o.fecha 
                               FROM hist_ordenes o 
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
                    ListaHistoricos.Add(new VentaHistoricaItem
                    {
                        Id = dr.GetInt32(0),
                        Cliente = dr.GetString(1),
                        Vendedor = dr.GetString(2),
                        Estado = est == "P" ? "Pagada" : "Cancelada",
                        Total = dr.GetDouble(4),
                        Fecha = dr.GetDateTime(5) // Mapeo de la fecha
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
                                               FROM hist_detordenes d 
                                               JOIN productos p ON d.idproducto = p.idproducto 
                                               WHERE d.idorden = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            using var dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
                ListaDetalle.Add(new DetalleItem { Producto = dr.GetString(0), Cantidad = dr.GetInt32(1), Subtotal = dr.GetDouble(2) });
        }

        private void btnRefrescar_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) { asbBusqueda.Text = ""; CargarHistoricos(); }
        private void asbBusqueda_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => CargarHistoricos(sender.Text);
        private void asbBusqueda_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) => sender.Text = args.SelectedItem.ToString();
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
    }
}