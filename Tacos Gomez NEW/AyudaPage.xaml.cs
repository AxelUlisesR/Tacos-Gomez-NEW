using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace Tacos_Gomez_NEW
{
    public sealed partial class AyudaPage : Page
    {
        public AyudaPage()
        {
            this.InitializeComponent();
            lvTemas.SelectedIndex = 0;
        }

        private async void lvTemas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvTemas.SelectedItem is ListViewItem item)
            {
                string tema = item.Content.ToString();
                await CargarArchivoMarkdown($"{tema}.md");
            }
        }

        private async Task CargarArchivoMarkdown(string nombreArchivo)
        {
            try
            {
                string rutaBase = Package.Current.InstalledLocation.Path;
                string rutaFinal = Path.Combine(rutaBase, "Assets", "Ayuda", nombreArchivo);

                if (File.Exists(rutaFinal))
                {
                    string contenido = await File.ReadAllTextAsync(rutaFinal);
                    markdownViewer.Text = contenido;
                }
                else
                {
                    markdownViewer.Text = $"# Error de Archivo\nNo se pudo encontrar el manual solicitado.\n\n**Archivo:** {nombreArchivo}\n**Ruta:** {rutaFinal}";
                }
            }
            catch (Exception ex)
            {
                markdownViewer.Text = $"# Error Crítico\n{ex.Message}";
            }
        }
    }
}