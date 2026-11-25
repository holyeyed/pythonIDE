using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace PythonEditor
{
    public partial class PipInstallDialog : Window
    {
        private readonly string _pythonExe;
        private bool _isInstalling = false;

        public PipInstallDialog(string pythonExe = "python")
        {
            InitializeComponent();
            _pythonExe = string.IsNullOrWhiteSpace(pythonExe) ? "python" : pythonExe;
            Loaded += (_, __) => txtPackageName.Focus();
        }

        private void TxtPackageName_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isInstalling)
            {
                e.Handled = true;
                BtnInstall_Click(sender, e);
            }
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_isInstalling) return;

            string packageArgs = txtPackageName.Text.Trim();
            if (string.IsNullOrWhiteSpace(packageArgs))
            {
                MessageBox.Show("Nhập tên gói cần cài (VD: requests, pillow, rich)!",
                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPackageName.Focus();
                return;
            }

            // Khóa khung nhập và nút Install khi đang chạy
            _isInstalling = true;
            SetInputLocked(true);

            AppendInstallLog($"\n> {_pythonExe} -m pip install {packageArgs}");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonExe,
                    Arguments = $"-m pip install {packageArgs}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = startInfo };
                process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        AppendInstallLog(args.Data);
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        AppendInstallLog(args.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0)
                    AppendInstallLog($"[OK] Xong '{packageArgs}'.");
                else
                    AppendInstallLog($"[FAIL] Kết thúc với mã lỗi {process.ExitCode}.");
            }
            catch (Exception ex)
            {
                AppendInstallLog($"[ERROR] {ex.Message}");
            }
            finally
            {
                // Xóa text ô nhập gói, mở khóa và trả focus để nhập gói tiếp theo
                txtPackageName.Clear();
                SetInputLocked(false);
                _isInstalling = false;
                txtPackageName.Focus();
            }
        }

        private void SetInputLocked(bool locked)
        {
            txtPackageName.IsEnabled = !locked;
            btnInstall.IsEnabled = !locked;
        }

        private void AppendInstallLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtInstallOutput.AppendText(message + Environment.NewLine);
                txtInstallOutput.ScrollToEnd();
            });
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_isInstalling)
            {
                MessageBox.Show("Đang cài đặt gói, vui lòng chờ hoàn tất!",
                    "Đang xử lý", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Close();
        }
    }
}