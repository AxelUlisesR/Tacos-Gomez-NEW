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
using SkiaSharp;
using LiveChartsCore.SkiaSharpView.Painting;

namespace Tacos_Gomez_NEW
{
    public sealed partial class GeneradorReportes : Page
    {
        private string cadena = "Host=localhost;Port=5432;Database=Taqueria;Username=usuario;Password=1234;";
        private DataTable datosReporte;
        private string tituloActual = "";
        private bool mostrarGrafica = false;
        private List<string> listaClientes = new List<string>();

        // Paleta de colores manual para evitar errores de librerías
        private readonly SKColor[] misColores = new SKColor[]
        {
            SKColors.Crimson, SKColors.DodgerBlue, SKColors.LimeGreen,
            SKColors.Orange, SKColors.MediumPurple, SKColors.Gold,
            SKColors.DeepPink, SKColors.Cyan, SKColors.IndianRed, SKColors.LightSeaGreen
        };

        public GeneradorReportes()
        {
            this.InitializeComponent();
            QuestPDF.Settings.License = LicenseType.Community;
            dtpFin.Date = DateTimeOffset.Now;
            dtpInicio.Date = DateTimeOffset.Now.AddDays(-30);
            _ = CargarListaClientes();
        }

        private async Task CargarListaClientes()
        {
            try
            {
                using var conn = new NpgsqlConnection(cadena); await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT nombre FROM clientes ORDER BY nombre", conn);
                using var dr = await cmd.ExecuteReaderAsync();
                while (await dr.ReadAsync()) listaClientes.Add(dr.GetString(0));
            }
            catch { }
        }

        private void cbReportes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (asbCliente == null) return;
            asbCliente.Visibility = cbReportes.SelectedIndex == 7 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void asbCliente_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                sender.ItemsSource = listaClientes.Where(c => c.ToLower().Contains(sender.Text.ToLower())).ToList();
        }

        private void asbCliente_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            sender.Text = args.SelectedItem.ToString();
        }

        private async void btnGenerar_Click(object sender, RoutedEventArgs e)
        {
            if (cbReportes.SelectedIndex == -1) return;
            DateTime f1 = dtpInicio.Date.Value.DateTime.Date;
            DateTime f2 = dtpFin.Date.Value.DateTime.Date;
            tituloActual = (cbReportes.SelectedItem as ComboBoxItem).Content.ToString();

            string sql = cbReportes.SelectedIndex switch
            {
                0 => "SELECT idorden, c.nombre as cliente, e.nombre as empleado, fecha, total FROM ordenes o JOIN clientes c USING(idcliente) JOIN empleados e USING(idempleado) WHERE fecha BETWEEN @f1 AND @f2",
                1 => "SELECT p.nombre, SUM(d.cantidad) as cantidad_total FROM detordenes d JOIN productos p USING(idproducto) JOIN ordenes o USING(idorden) WHERE o.fecha BETWEEN @f1 AND @f2 GROUP BY p.nombre ORDER BY cantidad_total DESC LIMIT 10",
                2 => "SELECT idorden, fecha, total, estado FROM ordenes WHERE fecha BETWEEN @f1 AND @f2",
                3 => "SELECT to_char(fecha, 'YYYY-MM') as mes, SUM(total) as ingresos FROM ordenes GROUP BY mes ORDER BY mes DESC",
                4 => "SELECT c.nombre as cliente, COUNT(idorden) as visitas FROM ordenes o JOIN clientes c USING(idcliente) WHERE fecha BETWEEN @f1 AND @f2 GROUP BY c.nombre ORDER BY visitas DESC LIMIT 10",
                5 => "SELECT e.nombre as empleado, SUM(o.total) as venta_total FROM ordenes o JOIN empleados e USING(idempleado) WHERE o.fecha BETWEEN @f1 AND @f2 GROUP BY e.nombre ORDER BY venta_total DESC LIMIT 10",
                6 => "SELECT categoria, SUM(subtotal) as total FROM detordenes JOIN productos USING(idproducto) JOIN ordenes USING(idorden) WHERE fecha BETWEEN @f1 AND @f2 GROUP BY categoria",
                7 => "SELECT o.fecha, e.nombre as empleado, o.total FROM ordenes o JOIN clientes c USING(idcliente) JOIN empleados e USING(idempleado) WHERE c.nombre = @cli AND o.fecha BETWEEN @f1 AND @f2 ORDER BY o.fecha DESC",
                8 => "SELECT c.nombre as cliente, AVG(total) as promedio FROM ordenes JOIN clientes c USING(idcliente) WHERE fecha BETWEEN @f1 AND @f2 GROUP BY c.nombre ORDER BY promedio DESC LIMIT 10",
                9 => "SELECT CASE WHEN estado = 'P' THEN 'Pagada' WHEN estado = 'R' THEN 'Registrada' ELSE 'Otras/Bajas' END as est, COUNT(*) as cantidad FROM ordenes WHERE fecha BETWEEN @f1 AND @f2 GROUP BY est",
                _ => ""
            };

            await CargarDatos(sql, f1, f2, asbCliente.Text);
        }

        private async Task CargarDatos(string sql, DateTime f1, DateTime f2, string cliente = "")
        {
            try
            {
                datosReporte = new DataTable();
                using (var conn = new NpgsqlConnection(cadena))
                {
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@f1", f1); cmd.Parameters.AddWithValue("@f2", f2);
                    if (cbReportes.SelectedIndex == 7) cmd.Parameters.AddWithValue("@cli", cliente);
                    using var reader = await cmd.ExecuteReaderAsync();
                    datosReporte.Load(reader);
                }

                dgVentas.ItemsSource = null; dgVentas.Columns.Clear();

                if (datosReporte.Rows.Count > 0)
                {
                    foreach (DataColumn col in datosReporte.Columns)
                    {
                        var binding = new Binding { Path = new PropertyPath("[" + col.ColumnName + "]") };
                        if (EsCampoDinero(col.ColumnName)) binding.Converter = (IValueConverter)this.Resources["MonedaConverter"];
                        dgVentas.Columns.Add(new DataGridTextColumn { Header = col.ColumnName, Binding = binding });
                    }
                    dgVentas.ItemsSource = datosReporte.DefaultView;
                    await Task.Delay(150);
                    ActualizarGraficaUI();
                }
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private bool EsCampoDinero(string nombreCol)
        {
            string n = nombreCol.ToLower();
            if (n == "ventas" || n == "cantidad" || n == "visitas" || n == "total_vendido" || n.Contains("cantidad")) return false;
            return n.Contains("total") || n.Contains("ingreso") || n.Contains("venta") || n.Contains("promedio") || n.Contains("subtotal") || n.Contains("precio");
        }

        private void ActualizarGraficaUI()
        {
            graficaPastel.Visibility = Visibility.Collapsed;
            graficaBarras.Visibility = Visibility.Collapsed;
            graficaPastel.Series = null;
            graficaBarras.Series = null;
            graficaBarras.XAxes = null;
            graficaBarras.YAxes = null;

            int[] indicesConGrafica = { 1, 3, 4, 5, 6, 7, 8, 9 };
            mostrarGrafica = indicesConGrafica.Contains(cbReportes.SelectedIndex);

            if (!mostrarGrafica || datosReporte.Rows.Count == 0) return;

            if (tituloActual.Contains("Categoria") || tituloActual.Contains("Productos") || tituloActual.Contains("Eficiencia") || cbReportes.SelectedIndex == 9)
            {
                graficaPastel.Series = GetSeriesPastel();
                graficaPastel.Visibility = Visibility.Visible;
            }
            else
            {
                ConfigurarGraficaBarras();
                graficaBarras.Visibility = Visibility.Visible;
            }
        }

        private List<ISeries> GetSeriesPastel() => datosReporte.AsEnumerable()
            .Select(r => new PieSeries<double> { Values = new[] { Convert.ToDouble(r[1]) }, Name = r[0].ToString() }).Cast<ISeries>().ToList();

        private void ConfigurarGraficaBarras()
        {
            var nombres = datosReporte.AsEnumerable().Select(r => r[0].ToString()).ToArray();
            var valores = datosReporte.AsEnumerable().Select(r => Convert.ToDouble(r[datosReporte.Columns.Count - 1])).ToArray();

            var serieRanking = new RowSeries<double>
            {
                Values = valores,
                Name = "Monto",
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                DataLabelsFormatter = point => point.Coordinate.PrimaryValue.ToString("N0")
            };

            serieRanking.PointMeasured += (point) =>
            {
                var visual = point.Visual;
                if (visual == null) return;
                var colorIndex = point.Index % misColores.Length;
                visual.Fill = new SolidColorPaint(misColores[colorIndex]);
            };

            graficaBarras.Series = new ISeries[] { serieRanking };
            graficaBarras.YAxes = new Axis[] { new Axis { Labels = nombres, LabelsPaint = new SolidColorPaint(SKColors.LightGray) } };
            graficaBarras.XAxes = new Axis[] { new Axis { Labeler = v => v.ToString("C0"), LabelsPaint = new SolidColorPaint(SKColors.Gray) } };
        }

        private byte[] GetBytesDeGrafica()
        {
            if (!mostrarGrafica) return null;
            if (tituloActual.Contains("Categoria") || tituloActual.Contains("Productos") || tituloActual.Contains("Eficiencia") || cbReportes.SelectedIndex == 9)
                return GetImage(new SKPieChart { Series = GetSeriesPastel() });
            else
                return GetImage(new SKCartesianChart { Series = graficaBarras.Series, YAxes = graficaBarras.YAxes, XAxes = graficaBarras.XAxes });
        }

        private byte[] GetImage(SKPieChart chart) { using var ms = new MemoryStream(); chart.SaveImage(ms); return ms.ToArray(); }
        private byte[] GetImage(SKCartesianChart chart) { using var ms = new MemoryStream(); chart.SaveImage(ms); return ms.ToArray(); }

        private async void btnExcel_Click(object sender, RoutedEventArgs e)
        {
            if (datosReporte == null) return;
            try
            {
                string nombreArchivo = $"Reporte_{tituloActual.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                string ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), nombreArchivo);

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Datos");

                int filaInicio = 1;
                if (cbReportes.SelectedIndex == 7 && !string.IsNullOrEmpty(asbCliente.Text))
                {
                    ws.Cell(1, 1).Value = "REPORTE HISTÓRICO DEL CLIENTE: " + asbCliente.Text;
                    ws.Cell(1, 1).Style.Font.Bold = true;
                    ws.Cell(1, 1).Style.Font.FontSize = 14;
                    filaInicio = 3;
                }

                ws.Cell(filaInicio, 1).InsertTable(datosReporte);
                for (int i = 0; i < datosReporte.Columns.Count; i++)
                    if (EsCampoDinero(datosReporte.Columns[i].ColumnName)) ws.Column(i + 1).Style.NumberFormat.Format = "$#,##0.00";

                byte[] img = GetBytesDeGrafica();
                if (img != null)
                {
                    using var ms = new MemoryStream(img);
                    ws.AddPicture(ms).MoveTo(ws.Cell(datosReporte.Rows.Count + filaInicio + 2, 1)).Scale(0.6);
                }
                wb.SaveAs(ruta);
                await MostrarMensaje("Éxito", "Excel guardado en el escritorio.");
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private async void btnPDF_Click(object sender, RoutedEventArgs e)
        {
            if (datosReporte == null) return;
            try
            {
                string nombreArchivo = $"Reporte_{tituloActual.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string ruta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), nombreArchivo);
                byte[] img = mostrarGrafica ? GetBytesDeGrafica() : null;
                string nombreCli = asbCliente.Text;
                bool esReporteCliente = cbReportes.SelectedIndex == 7;

                Document.Create(c => {
                    c.Page(p => {
                        p.Margin(1, Unit.Centimetre);
                        p.Header().Column(headerCol => {
                            headerCol.Item().Text("TACOS GÓMEZ - " + tituloActual).FontSize(20).FontColor(Colors.Red.Medium).SemiBold();
                            if (esReporteCliente && !string.IsNullOrEmpty(nombreCli))
                                headerCol.Item().PaddingTop(5).Text("CLIENTE: " + nombreCli).FontSize(14).Italic();
                            headerCol.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        });

                        p.Content().Column(col => {
                            col.Item().PaddingTop(10).Table(t => {
                                t.ColumnsDefinition(cd => { for (int i = 0; i < datosReporte.Columns.Count; i++) cd.RelativeColumn(); });
                                t.Header(h => { foreach (DataColumn dc in datosReporte.Columns) h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text(dc.ColumnName).SemiBold(); });
                                foreach (DataRow r in datosReporte.Rows)
                                    for (int i = 0; i < datosReporte.Columns.Count; i++)
                                    {
                                        string valor = r[i]?.ToString();
                                        if (EsCampoDinero(datosReporte.Columns[i].ColumnName)) valor = string.Format("${0:N2}", r[i]);
                                        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(valor);
                                    }
                            });
                            if (img != null) col.Item().PaddingTop(20).AlignCenter().Width(350).Image(img);
                        });
                        p.Footer().AlignCenter().Text(x => { x.Span("Página "); x.CurrentPageNumber(); });
                    });
                }).GeneratePdf(ruta);
                await MostrarMensaje("Éxito", "PDF guardado en el escritorio.");
            }
            catch (Exception ex) { await MostrarMensaje("Error", ex.Message); }
        }

        private async Task MostrarMensaje(string t, string c) { ContentDialog d = new ContentDialog { Title = t, Content = c, CloseButtonText = "OK", XamlRoot = this.XamlRoot }; await d.ShowAsync(); }
    }

    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null && double.TryParse(value.ToString(), out double dv))
            {
                return string.Format("${0:N2}", dv);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}