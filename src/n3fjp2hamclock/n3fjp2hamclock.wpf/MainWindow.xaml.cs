using n3fjp2hamclock.helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace n3fjp2hamclock.wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ApiClient? _apiClient;
        private LogLevel _minLogLevel = LogLevel.Info;

        public MainWindow()
        {
            InitializeComponent();
        }

        internal void LogMessage(string message, LogLevel logLevel = LogLevel.Info)
        {
            if (logLevel < _minLogLevel)
            {
                return;
            }

            txtConsole.Text += message + "\r\n";
            txtConsole.ScrollToEnd();
        }

        public async Task Connect()
        {
            txtConsole.Text = "";
            EnableDisableUiElements(false);
            Properties.Settings.Default.Save();

            try
            {
                _apiClient = new ApiClient(
                    txtApiServerHost.Text,
                    int.Parse(txtApiServerPort.Text),
                    txtHamClockUris.Text,
                    new Logger(this)
                );
                btnConnect.Content = "Disconnect";

                await _apiClient.Connect();
            }
            catch (Exception ex)
            {
                LogMessage("Error: " + ex.Message, LogLevel.Error);
                Disconnect();
            }
        }

        private void Disconnect()
        {
            if (_apiClient == null)
            {
                return;
            }
            _apiClient.Disconnect();
            _apiClient = null;

            btnConnect.Content = "Connect";
            EnableDisableUiElements(true);
        }

        private void EnableDisableUiElements(bool enabled)
        {
            btnConnect.IsEnabled = true; // Always enable button

            ckTrace.IsEnabled = enabled;
            txtApiServerHost.IsEnabled = enabled;
            txtApiServerPort.IsEnabled = enabled;
            txtHamClockUris.IsEnabled = enabled;
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            // Prevent double clicks
            btnConnect.IsEnabled = false;

            if (_apiClient == null)
            {
                await Connect();
            }
            else
            {
                Disconnect();
            }
        }

        private void ckTrace_Checked(object sender, RoutedEventArgs e)
        {
            _minLogLevel = LogLevel.Trace;
        }

        private void ckTrace_Unchecked(object sender, RoutedEventArgs e)
        {
            _minLogLevel = LogLevel.Info;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.Save();
            Disconnect();
        }

        private void txtApiServerPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Validate port number is numeric
            if (int.TryParse(txtApiServerPort.Text, out int portNumber))
            {
                txtApiServerPort.Background = Brushes.White;

                // Verify port range
                if (portNumber < 1 || portNumber > 65535)
                {
                    // Show dialog
                    MessageBox.Show("Port must be between 1 and 65535.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    txtApiServerPort.Background = Brushes.Red;

                    // Select all text
                    txtApiServerPort.SelectAll();
                }
            }
            else
            {
                // Show dialog
                MessageBox.Show("Port must be a number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                txtApiServerPort.Background = Brushes.Red;

                // Select all text
                txtApiServerPort.SelectAll();

                return;
            }
        }
    }
}