using Microsoft.UI.Xaml;

namespace Tacos_Gomez_NEW
{
    public partial class App : Application
    {
        // Esta variable estática es la que permite que SettingsPage abra el explorador de archivos
        public static Window m_window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Creamos la ventana principal (MainWindow o LoginPage, según tu flujo)
            // y la guardamos en la variable estática m_window
            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}