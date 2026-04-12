using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Npgsql;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.SKCharts;
using CommunityToolkit.WinUI.UI.Controls;

namespace Tacos_Gomez_NEW
{
    public sealed partial class GeneradorReportes : Page
    {
        private string cadena = "Host=localhost;Port=5432;Database=Taqueria;Username=consulta_taqueria;Password=Cons123;";
        private DataTable datosReporte;
        private string tituloActual = "";
        private bool mostrarGrafica = false;

        public GeneradorReportes()
        {
            this.InitializeComponent();
            QuestPDF.Settings.License = LicenseType.Community;
            dtpFin.Date = DateTimeOffset.Now;
            dtpInicio.Date = DateTimeOffset.Now.AddDays(-7);
        }

        private async void btnGenerar_Click(object sender, RoutedEventArgs e)
        {
            if (cbReportes.SelectedIndex == -1) return;
            DateTime f1 = dtpInicio.Date.Value.DateTime.Date;
            DateTime f2 = dtpFin.Date.Value.DateTime.Date;
            tituloActual = (cbReportes.SelectedItem as ComboBoxItem).Content.ToString();

            string sql = cbReportes.SelectedIndex switch
            {
                0 => "SELECT idorden, c.nombre as cliente, fecha, total FROM ordenes o JOIN clientes c USING(idcliente) WHERE fecha BETWEEN @f1 AND @f2",
                1 => "SELECT p.nombre, SUM(d.cantidad) as total FROM detordenes d JOIN productos p USING(idproducto) JOIN ordenes o USING(idorden) WHERE o.fecha BETWEEN @f1 AND @f2 GROUP BY p.nombre ORDER BY total DESC LIMIT 10",
                2 => "SELECT idorden, fecha, total, estado FROM ordenes WHERE fecha BETWEEN @f1 AND @f2",
                3 => "SELECT to_char(fecha, 'YYYY-MM') as mes, SUM(total) as ingresos FROM ordenes GROUP BY mes ORDER BY mes DESC",
                4 => "SELECT c.nombre as cliente, COUNT(idorden) as visitas FROM ordenes o JOIN clientes c USING(idcliente) WHERE fecha BETWEEN @f1 AND @f2 GROUP BY c.nombre ORDER BY visitas DESC LIMIT 10",
                5 => "SELECT e.nombre as empleado, SUM(total) as venta_total FROM ordenes o JOIN empleados e USING(idempleado) WHERE o.fecha BETWEEN @f1 AND @f2 GROUP BY e.nombre",
                6 => "SELECT categoria, SUM(subtotal) as total FROM detordenes JOIN productos USING(idproducto) JOIN ordenes USING(idorden) WHERE fecha BETWEEN @f1 AND @f2 GROUP BY categoria",
                7 => "SELECT c.nombre as cliente, AVG(total) as promedio FROM ordenes JOIN clientes c USING(idcliente) WHERE fecha BETWEEN @f1 AND @f2 GROUP BY c.nombre",
                8 => "SELECT to_char(fecha, 'Day') as dia, COUNT(*) as ventas FROM ordenes WHERE fecha BETWEEN @f1 AND @f2 GROUP BY dia",
                9 => "SELECT CASE WHEN estado = 'R' THEN 'Activas' ELSE 'Otras/Bajas' END as est, COUNT(*) FROM ordenes WHERE fecha BETWEEN @f1 AND @f2 GROUP BY est",
                _ => ""
            };

            await CargarDatos(sql, f1, f2);
        }

        private async Task CargarDatos(string sql, DateTime f1, DateTime f2)
        {
            try
            {
                datosReporte = new DataTable();
                using (var conn = new NpgsqlConnection(cadena))
                {
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@f1", f1); cmd.Parameters.AddWithValue("@f2", f2);
                    using var reader = await cmd.ExecuteReaderAsync();
                    datosReporte.Load(reader);
                }
                dgVentas.ItemsSource = null; dgVentas.Columns.Clear();
                if (datosReporte.Rows.Count > 0)
                {
                    foreach (DataColumn col in datosReporte.Columns)
                    {
                        var binding = new Binding { Path = new PropertyPath("[" + col.ColumnName + "]") };
                        if (EsCampoDinero(col.ColumnName))
                            binding.Converter = (IValueConverter)this.Resources["MonedaConverter"];

                        var dgc = new DataGridTextColumn { Header = col.ColumnName, Binding = binding };
                        dgVentas.Columns.Add(dgc);
                    }
                    dgVentas.ItemsSource = datosReporte.DefaultView;
                    ActualizarGraficaUI();
                }
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private bool EsCampoDinero(string nombreCol)
        {
            string n = nombreCol.ToLower();
            // Evitamos que "ventas" (conteo) entre aquí, pero que "venta_total" o "total" sí.
            if (n == "ventas" || n == "cantidad" || n == "visitas") return false;
            return n.Contains("total") || n.Contains("ingreso") || n.Contains("venta") || n.Contains("promedio") || n.Contains("subtotal") || n.Contains("precio");
        }

        private void ActualizarGraficaUI()
        {
            graficaPastel.Visibility = Visibility.Collapsed;
            graficaBarras.Visibility = Visibility.Collapsed;
            mostrarGrafica = !(cbReportes.SelectedIndex == 0 || cbReportes.SelectedIndex == 2);
            if (!mostrarGrafica) return;
            if (tituloActual.Contains("Categoria") || tituloActual.Contains("Productos") || tituloActual.Contains("Eficiencia"))
            {
                graficaPastel.Visibility = Visibility.Visible; graficaPastel.Series = GetSeriesPastel();
            }
            else
            {
                graficaBarras.Visibility = Visibility.Visible; graficaBarras.Series = GetSeriesBarras();
            }
        }

        private List<ISeries> GetSeriesPastel() => datosReporte.AsEnumerable().Select(r => new PieSeries<double> { Values = new[] { Convert.ToDouble(r[1]) }, Name = r[0].ToString() }).Cast<ISeries>().ToList();
        private List<ISeries> GetSeriesBarras() => datosReporte.AsEnumerable().Select(r => new ColumnSeries<double> { Values = new[] { Convert.ToDouble(r[datosReporte.Columns.Count - 1]) }, Name = r[0].ToString() }).Cast<ISeries>().ToList();

        private byte[] GetBytesDeGrafica()
        {
            if (tituloActual.Contains("Categoria") || tituloActual.Contains("Eficiencia") || tituloActual.Contains("Productos"))
                return GetImage(new SKPieChart { Series = GetSeriesPastel(), LegendPosition = LiveChartsCore.Measure.LegendPosition.Right });
            else
                return GetImage(new SKCartesianChart { Series = GetSeriesBarras(), LegendPosition = LiveChartsCore.Measure.LegendPosition.Bottom });
        }

        private byte[] GetImage(SKPieChart chart) { using var ms = new MemoryStream(); chart.SaveImage(ms); return ms.ToArray(); }
        private byte[] GetImage(SKCartesianChart chart) { using var ms = new MemoryStream(); chart.SaveImage(ms); return ms.ToArray(); }

        private async void btnExcel_Click(object sender, RoutedEventArgs e)
        {
            if (datosReporte == null) return;
            try
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string nombre = $"Reporte_{tituloActual.Replace(" ", "_")}_{ts}.xlsx";
                string ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), nombre);
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Datos");
                ws.Cell(1, 1).InsertTable(datosReporte);
                for (int i = 0; i < datosReporte.Columns.Count; i++)
                {
                    if (EsCampoDinero(datosReporte.Columns[i].ColumnName))
                        ws.Column(i + 1).Style.NumberFormat.Format = "$#,##0.00";
                }
                if (mostrarGrafica)
                {
                    byte[] img = GetBytesDeGrafica();
                    if (img != null) { using var ms = new MemoryStream(img); ws.AddPicture(ms).MoveTo(ws.Cell(datosReporte.Rows.Count + 3, 1)).Scale(0.6); }
                }
                wb.SaveAs(ruta);
                await MostrarMensaje("Éxito", $"Excel {nombre} guardado.");
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private async void btnPDF_Click(object sender, RoutedEventArgs e)
        {
            if (datosReporte == null) return;
            try
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string nombre = $"Reporte_{tituloActual.Replace(" ", "_")}_{ts}.pdf";
                string ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), nombre);
                byte[] img = mostrarGrafica ? GetBytesDeGrafica() : null;
                Document.Create(c => {
                    c.Page(p => {
                        p.Margin(1, Unit.Centimetre);
                        p.Header().Text("TACOS GÓMEZ - " + tituloActual).FontSize(20).FontColor(Colors.Red.Medium);
                        p.Content().Column(col => {
                            col.Item().Table(t => {
                                t.ColumnsDefinition(cd => { for (int i = 0; i < datosReporte.Columns.Count; i++) cd.RelativeColumn(); });
                                t.Header(h => {
                                    foreach (DataColumn dc in datosReporte.Columns)
                                        h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text(dc.ColumnName).SemiBold();
                                });
                                foreach (DataRow r in datosReporte.Rows)
                                {
                                    for (int i = 0; i < datosReporte.Columns.Count; i++)
                                    {
                                        string valor = r[i]?.ToString();
                                        if (EsCampoDinero(datosReporte.Columns[i].ColumnName))
                                            valor = string.Format("${0:N2}", r[i]);
                                        t.Cell().BorderBottom(1).Padding(5).Text(valor);
                                    }
                                }
                            });
                            if (img != null) col.Item().PaddingTop(20).AlignCenter().Width(350).Image(img);
                        });
                    });
                }).GeneratePdf(ruta);
                await MostrarMensaje("Éxito", $"PDF {nombre} guardado.");
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private async Task MostrarMensaje(string t, string c)
        {
            ContentDialog d = new ContentDialog { Title = t, Content = c, CloseButtonText = "OK", XamlRoot = this.XamlRoot }; await d.ShowAsync();
        }
    }

    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null && double.TryParse(value.ToString(), out double doubleValue))
                return string.Format("${0:N2}", doubleValue);
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}