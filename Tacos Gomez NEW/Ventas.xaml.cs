using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Npgsql;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Tacos_Gomez_NEW
{
    public class DetalleVenta
    {
        public string Nombre { get; set; }
        public double Precio { get; set; }
        public int Cantidad { get; set; }
        public double Importe { get; set; }
        public int NoPlato { get; set; }
        public string PrecioFormateado => Precio.ToString("C2");
        public string ImporteFormateado => Importe.ToString("C2");
    }

    public sealed partial class VentasPage : Page
    {
        private string cadena = "Host=localhost;Port=5432;Database=Taqueria;Username=usuario;Password=1234;";
        private ObservableCollection<DetalleVenta> ListaMenu = new ObservableCollection<DetalleVenta>();
        private List<string> listaClientes = new List<string>();

        double subtotalValue = 0, ivaValue = 0, totalValue = 0;
        int platoActual = 1, tacosEnPlato = 0;
        const int MAX_TACOS_POR_PLATO = 8;

        public VentasPage()
        {
            this.InitializeComponent();
            dgvMenu.ItemsSource = ListaMenu;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) => _ = CargarDatosIniciales();

        private async Task CargarDatosIniciales()
        {
            try
            {
                using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
                cbEmpleado.Items.Clear(); cboProducto.Items.Clear(); listaClientes.Clear();
                using (var cmd = new NpgsqlCommand("SELECT nombre FROM clientes ORDER BY nombre", conn))
                using (var dr = await cmd.ExecuteReaderAsync()) while (await dr.ReadAsync()) listaClientes.Add(dr.GetString(0));
                using (var cmd = new NpgsqlCommand("SELECT nombre FROM empleados ORDER BY nombre", conn))
                using (var dr = await cmd.ExecuteReaderAsync()) while (await dr.ReadAsync()) cbEmpleado.Items.Add(dr.GetString(0));
                using (var cmd = new NpgsqlCommand("SELECT nombre FROM productos ORDER BY nombre", conn))
                using (var dr = await cmd.ExecuteReaderAsync()) while (await dr.ReadAsync()) cboProducto.Items.Add(dr.GetString(0));
                EstadoInicial();
            }
            catch (Exception ex) { _ = MostrarMensaje("Error DB", ex.Message); }
        }

        private void asbCliente_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                sender.ItemsSource = listaClientes.Where(c => c.ToLower().Contains(sender.Text.ToLower())).ToList();
        }

        private async void asbCliente_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            try
            {
                using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT direccion, telefono, numero FROM clientes WHERE nombre = @n", conn);
                cmd.Parameters.AddWithValue("n", args.SelectedItem.ToString());
                using var dr = await cmd.ExecuteReaderAsync();
                if (await dr.ReadAsync())
                {
                    txtDireccion.Text = dr["direccion"].ToString();
                    txtTelefono.Text = dr["telefono"].ToString();
                    txtNumCasa.Text = dr["numero"].ToString();
                }
            }
            catch { }
        }

        private async void cbEmpleado_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbEmpleado.SelectedItem == null) return;
            using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT rol FROM empleados WHERE nombre = @n", conn);
            cmd.Parameters.AddWithValue("n", cbEmpleado.SelectedItem.ToString());
            txtRol.Text = (await cmd.ExecuteScalarAsync())?.ToString();
        }

        private async void cboProducto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboProducto.SelectedItem == null) return;
            using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT precio FROM productos WHERE nombre = @n", conn);
            cmd.Parameters.AddWithValue("n", cboProducto.SelectedItem.ToString());
            nbPrecio.Value = Convert.ToDouble(await cmd.ExecuteScalarAsync());
        }

        private void cmdAceptar_Click(object sender, RoutedEventArgs e)
        {
            if (cboProducto.SelectedItem == null || nbCantidad.Value < 1) return;
            int canRestante = (int)nbCantidad.Value; double precio = nbPrecio.Value;
            while (canRestante > 0)
            {
                int espacio = MAX_TACOS_POR_PLATO - tacosEnPlato;
                if (espacio <= 0) { platoActual++; tacosEnPlato = 0; espacio = MAX_TACOS_POR_PLATO; }
                int aAgregar = Math.Min(canRestante, espacio);
                ListaMenu.Add(new DetalleVenta { Nombre = cboProducto.SelectedItem.ToString(), Precio = precio, Cantidad = aAgregar, Importe = precio * aAgregar, NoPlato = platoActual });
                tacosEnPlato += aAgregar; canRestante -= aAgregar;
            }
            lblPlato.Text = $"Plato Actual: {platoActual}";
            CalcularTotales();
        }

        private void CalcularTotales()
        {
            subtotalValue = ListaMenu.Sum(x => x.Importe); ivaValue = subtotalValue * 0.16; totalValue = subtotalValue + ivaValue;
            txtSubtotal.Text = subtotalValue.ToString("C2"); txtIVA.Text = ivaValue.ToString("C2");
            txtTotal.Text = "TOTAL: " + totalValue.ToString("C2");
        }

        private async void btnGrabar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(asbCliente.Text) || cbEmpleado.SelectedItem == null || ListaMenu.Count == 0) return;
            try
            {
                using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
                using var trans = await conn.BeginTransactionAsync();
                int idC, idE;
                using (var cmd = new NpgsqlCommand("SELECT idcliente FROM clientes WHERE nombre=@n", conn)) { cmd.Parameters.AddWithValue("n", asbCliente.Text); idC = (int)await cmd.ExecuteScalarAsync(); }
                using (var cmd = new NpgsqlCommand("SELECT idempleado FROM empleados WHERE nombre=@n", conn)) { cmd.Parameters.AddWithValue("n", cbEmpleado.SelectedItem.ToString()); idE = (int)await cmd.ExecuteScalarAsync(); }

                int idO;
                using (var cmd = new NpgsqlCommand("INSERT INTO ordenes (idcliente, idempleado, fecha, total, estado) VALUES (@c,@e,CURRENT_DATE,@t,'R') RETURNING idorden", conn))
                { cmd.Parameters.AddWithValue("c", idC); cmd.Parameters.AddWithValue("e", idE); cmd.Parameters.AddWithValue("t", totalValue); idO = (int)await cmd.ExecuteScalarAsync(); }

                foreach (var d in ListaMenu)
                {
                    using var cmd = new NpgsqlCommand("INSERT INTO detordenes (idorden, idproducto, cantidad, subtotal, plato) VALUES (@o, (SELECT idproducto FROM productos WHERE nombre=@np LIMIT 1), @can, @sub, @pla)", conn);
                    cmd.Parameters.AddWithValue("o", idO); cmd.Parameters.AddWithValue("np", d.Nombre); cmd.Parameters.AddWithValue("can", d.Cantidad); cmd.Parameters.AddWithValue("sub", d.Importe); cmd.Parameters.AddWithValue("pla", d.NoPlato);
                    await cmd.ExecuteNonQueryAsync();
                }
                await trans.CommitAsync();

                ImprimirTicketFinal(idO, asbCliente.Text, cbEmpleado.SelectedItem.ToString(), txtDireccion.Text, txtNumCasa.Text, txtTelefono.Text);
                EstadoInicial();
            }
            catch (Exception ex) { _ = MostrarMensaje("Error", ex.Message); }
        }

        private void ImprimirTicketFinal(int folio, string cli, string emp, string dir, string num, string tel)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), $"Ticket_{folio}.pdf");
                string dirFull = $"{dir} #{num}";
                var items = ListaMenu.ToList();
                double subT = subtotalValue;
                double ivaT = ivaValue;
                double totalT = totalValue;

                Document.Create(container => {
                    container.Page(page => {
                        // AQUÍ ESTÁ EL TRUCO: Solo definimos el ancho (226pt), la altura se adapta sola
                        page.ContinuousSize(226, Unit.Point);
                        page.Margin(10);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.CourierNew));

                        page.Content().Column(col => {
                            // Encabezado
                            col.Item().AlignCenter().Text("TACOS GÓMEZ").FontSize(14).SemiBold();
                            col.Item().AlignCenter().Text("SAYULA, JALISCO");
                            col.Item().PaddingVertical(5).LineHorizontal(1);
                            col.Item().Text($"Folio: {folio}");
                            col.Item().Text($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}");
                            col.Item().PaddingVertical(2);
                            col.Item().Text("CLIENTE:").SemiBold();
                            col.Item().Text(cli);
                            col.Item().Text($"Dir: {dirFull}");
                            col.Item().Text($"Tel: {tel}");
                            col.Item().PaddingVertical(2);
                            col.Item().Text($"Atendió: {emp}");
                            col.Item().PaddingVertical(5).LineHorizontal(1);

                            // Tabla de productos
                            col.Item().Table(table => {
                                table.ColumnsDefinition(columns => { columns.ConstantColumn(20); columns.RelativeColumn(); columns.ConstantColumn(50); });
                                foreach (var item in items)
                                {
                                    table.Cell().Text(item.Cantidad.ToString());
                                    table.Cell().Text(item.Nombre);
                                    table.Cell().AlignRight().Text(item.Importe.ToString("N2"));
                                }
                            });

                            // Totales
                            col.Item().PaddingTop(10).LineHorizontal(1);
                            col.Item().AlignRight().Text($"Subtotal: {subT:C2}");
                            col.Item().AlignRight().Text($"IVA (16%): {ivaT:C2}");
                            col.Item().AlignRight().Text($"TOTAL: {totalT:C2}").FontSize(11).SemiBold();
                            col.Item().PaddingTop(10).AlignCenter().Text("¡GRACIAS POR SU PREFERENCIA!");
                        });
                    });
                }).GeneratePdf(path);

                var psi = new ProcessStartInfo(path) { UseShellExecute = true };
                try { psi.Verb = "print"; Process.Start(psi); }
                catch { psi.Verb = ""; Process.Start(psi); }
            }
            catch (Exception ex) { _ = MostrarMensaje("Error Ticket", ex.Message); }
        }

        private void EstadoInicial()
        {
            asbCliente.IsEnabled = cbEmpleado.IsEnabled = cboProducto.IsEnabled = nbCantidad.IsEnabled = cmdAceptar.IsEnabled = btnGrabar.IsEnabled = false;
            btnNuevo.IsEnabled = true; ListaMenu.Clear(); platoActual = 1; tacosEnPlato = 0;
            txtSubtotal.Text = txtIVA.Text = "$0.00"; txtTotal.Text = "TOTAL: $0.00";
            asbCliente.Text = txtDireccion.Text = txtRol.Text = txtTelefono.Text = txtNumCasa.Text = "";
            lblPlato.Text = "Plato Actual: 1";
        }

        private async void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            asbCliente.IsEnabled = cbEmpleado.IsEnabled = cboProducto.IsEnabled = nbCantidad.IsEnabled = cmdAceptar.IsEnabled = btnGrabar.IsEnabled = true;
            btnNuevo.IsEnabled = false;
            try
            {
                using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(idorden), 0) + 1 FROM ordenes", conn);
                txtId.Text = "ID: " + (await cmd.ExecuteScalarAsync()).ToString();
            }
            catch { }
        }

        private async Task MostrarMensaje(string t, string c)
        {
            ContentDialog d = new ContentDialog { Title = t, Content = c, CloseButtonText = "OK", XamlRoot = this.XamlRoot };
            await d.ShowAsync();
        }
    }
}