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

        double subtotal = 0, iva = 0, total = 0;
        int platoActual = 1, tacosEnPlato = 0;
        const int MAX_TACOS_POR_PLATO = 8;

        public VentasPage()
        {
            this.InitializeComponent();
            dgvMenu.ItemsSource = ListaMenu;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _ = CargarDatosIniciales();
            EstadoInicial();
        }

        private async Task CargarDatosIniciales()
        {
            try
            {
                using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
                cbCliente.Items.Clear(); cbEmpleado.Items.Clear(); cboProducto.Items.Clear();

                using (var cmd = new NpgsqlCommand("SELECT nombre FROM clientes ORDER BY nombre", conn))
                using (var dr = await cmd.ExecuteReaderAsync()) while (await dr.ReadAsync()) cbCliente.Items.Add(dr.GetString(0));

                using (var cmd = new NpgsqlCommand("SELECT nombre FROM empleados ORDER BY nombre", conn))
                using (var dr = await cmd.ExecuteReaderAsync()) while (await dr.ReadAsync()) cbEmpleado.Items.Add(dr.GetString(0));

                using (var cmd = new NpgsqlCommand("SELECT nombre FROM productos ORDER BY nombre", conn))
                using (var dr = await cmd.ExecuteReaderAsync()) while (await dr.ReadAsync()) cboProducto.Items.Add(dr.GetString(0));
            }
            catch { }
        }

        private async void cbCliente_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbCliente.SelectedItem == null) return;
            using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT direccion FROM clientes WHERE nombre = @n", conn);
            cmd.Parameters.AddWithValue("n", cbCliente.SelectedItem.ToString());
            txtDireccion.Text = (await cmd.ExecuteScalarAsync())?.ToString();
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
                ListaMenu.Add(new DetalleVenta
                {
                    Nombre = cboProducto.SelectedItem.ToString(),
                    Precio = precio,
                    Cantidad = aAgregar,
                    Importe = precio * aAgregar,
                    NoPlato = platoActual
                });

                tacosEnPlato += aAgregar; canRestante -= aAgregar;
            }
            lblPlato.Text = $"Plato Actual: {platoActual}";
            CalcularTotales();
            nbCantidad.Value = 1;
        }

        private void CalcularTotales()
        {
            subtotal = ListaMenu.Sum(x => x.Importe); iva = subtotal * 0.16; total = subtotal + iva;
            txtSubtotal.Text = subtotal.ToString("C2");
            txtIVA.Text = iva.ToString("C2");
            txtTotal.Text = total.ToString("C2");
        }

        private async void btnGrabar_Click(object sender, RoutedEventArgs e)
        {
            if (cbCliente.SelectedItem == null || cbEmpleado.SelectedItem == null || ListaMenu.Count == 0) return;
            try
            {
                using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
                using var trans = await conn.BeginTransactionAsync();

                int idC, idE;
                using (var cmd = new NpgsqlCommand("SELECT idcliente FROM clientes WHERE nombre=@n", conn)) { cmd.Parameters.AddWithValue("n", cbCliente.SelectedItem.ToString()); idC = (int)await cmd.ExecuteScalarAsync(); }
                using (var cmd = new NpgsqlCommand("SELECT idempleado FROM empleados WHERE nombre=@n", conn)) { cmd.Parameters.AddWithValue("n", cbEmpleado.SelectedItem.ToString()); idE = (int)await cmd.ExecuteScalarAsync(); }

                int idO;
                using (var cmd = new NpgsqlCommand("INSERT INTO ordenes (idcliente, idempleado, fecha, total, estado) VALUES (@c,@e,CURRENT_DATE,@t,'R') RETURNING idorden", conn))
                { cmd.Parameters.AddWithValue("c", idC); cmd.Parameters.AddWithValue("e", idE); cmd.Parameters.AddWithValue("t", total); idO = (int)await cmd.ExecuteScalarAsync(); }

                foreach (var d in ListaMenu)
                {
                    using var cmd = new NpgsqlCommand("INSERT INTO detordenes (idorden, idproducto, cantidad, subtotal, plato) VALUES (@o, (SELECT idproducto FROM productos WHERE nombre=@np LIMIT 1), @can, @sub, @pla)", conn);
                    cmd.Parameters.AddWithValue("o", idO); cmd.Parameters.AddWithValue("np", d.Nombre); cmd.Parameters.AddWithValue("can", d.Cantidad); cmd.Parameters.AddWithValue("sub", d.Importe); cmd.Parameters.AddWithValue("pla", d.NoPlato);
                    await cmd.ExecuteNonQueryAsync();
                }
                await trans.CommitAsync();
                await new ContentDialog { Title = "Tacos Gomez", Content = "¡Orden cobrada con éxito!", CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync();
                EstadoInicial();
            }
            catch (Exception ex) { await new ContentDialog { Title = "Error", Content = ex.Message, CloseButtonText = "OK", XamlRoot = this.XamlRoot }.ShowAsync(); }
        }

        private void EstadoInicial()
        {
            cbCliente.IsEnabled = cbEmpleado.IsEnabled = cboProducto.IsEnabled = nbCantidad.IsEnabled = cmdAceptar.IsEnabled = btnGrabar.IsEnabled = false;
            btnNuevo.IsEnabled = true; ListaMenu.Clear(); platoActual = 1; tacosEnPlato = 0;
            txtSubtotal.Text = txtIVA.Text = txtTotal.Text = "$0.00";
            lblPlato.Text = "Plato Actual: 1";
        }

        private async void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            cbCliente.IsEnabled = cbEmpleado.IsEnabled = cboProducto.IsEnabled = nbCantidad.IsEnabled = cmdAceptar.IsEnabled = btnGrabar.IsEnabled = true;
            btnNuevo.IsEnabled = false;
            using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(idorden), 0) + 1 FROM ordenes", conn);
            txtId.Text = "ID: " + (await cmd.ExecuteScalarAsync()).ToString();
        }
    }
}