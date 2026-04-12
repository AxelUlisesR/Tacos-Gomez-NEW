using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System.Diagnostics;
using Windows.Storage.Pickers;

namespace Tacos_Gomez_NEW
{
    public sealed partial class SettingsPage : Page
    {
        // Ruta actualizada a la versión 17 de PostgreSQL
        private readonly string pgPath = @"C:\Program Files\PostgreSQL\17\bin";
        // Contraseña actualizada
        private readonly string pgPass = "Ramamos06";

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        private async void btnBackup_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("SQL Script", new List<string>() { ".sql" });
            savePicker.SuggestedFileName = $"Backup_Taqueria_{DateTime.Now:yyyyMMdd_HHmm}";

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    if (!Directory.Exists(pgPath))
                    {
                        await MostrarMensaje("Error de Configuración", $"La ruta de PostgreSQL no existe: {pgPath}");
                        return;
                    }

                    // Usando la contraseña Ramamos06
                    string comando = $"/c set PGPASSWORD={pgPass}&& \"{pgPath}\\pg_dump.exe\" -h localhost -U postgres -d Taqueria --column-inserts -f \"{file.Path}\"";

                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", comando)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    };

                    using (Process p = Process.Start(psi))
                    {
                        string errorOutput = await p.StandardError.ReadToEndAsync();
                        await p.WaitForExitAsync();

                        FileInfo info = new FileInfo(file.Path);

                        if (p.ExitCode == 0 && info.Exists && info.Length > 0)
                        {
                            await MostrarMensaje("Éxito", $"Respaldo creado: {info.Name}\nTamaño: {info.Length / 1024} KB");
                        }
                        else
                        {
                            await MostrarMensaje("Error en Respaldo", "No se pudo generar el archivo.\n\nDetalle: " + errorOutput);
                        }
                    }
                }
                catch (Exception ex) { await MostrarMensaje("Error de Sistema", ex.Message); }
            }
        }

        private async void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".sql");

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    if (!Directory.Exists(pgPath))
                    {
                        await MostrarMensaje("Error de Configuración", $"La ruta de PostgreSQL no existe: {pgPath}");
                        return;
                    }

                    // Usando la contraseña Ramamos06
                    string comando = $"/c set PGPASSWORD={pgPass}&& \"{pgPath}\\psql.exe\" -h localhost -U postgres -d Taqueria -f \"{file.Path}\"";

                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", comando)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    };

                    using (Process p = Process.Start(psi))
                    {
                        string errorOutput = await p.StandardError.ReadToEndAsync();
                        await p.WaitForExitAsync();

                        if (p.ExitCode == 0)
                        {
                            await MostrarMensaje("Restauración Exitosa", "La base de datos ha sido restaurada. Reinicia la aplicación para ver los cambios.");
                        }
                        else
                        {
                            await MostrarMensaje("Error en Restauración", "No se pudo restaurar.\n\nDetalle: " + errorOutput);
                        }
                    }
                }
                catch (Exception ex) { await MostrarMensaje("Error de Sistema", ex.Message); }
            }
        }

        private async Task MostrarMensaje(string titulo, string contenido)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = titulo,
                Content = contenido,
                CloseButtonText = "Entendido",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

  
}