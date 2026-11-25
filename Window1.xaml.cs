using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace PythonEditor
{
    public partial class NewProjectWindow : Window
    {
        public string CreatedProjectPath { get; private set; }
        private readonly string _projectsRoot;

        public NewProjectWindow(string projectsRoot)
        {
            InitializeComponent();
            _projectsRoot = projectsRoot;
            txtProjectName.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string projectName = txtProjectName.Text.Trim();
            string appName = txtAppName.Text.Trim();
            string mainClassFull = txtMainClass.Text.Trim(); // Có thể dùng làm tên file chính (VD: main.py)
            string author = txtAuthor.Text.Trim();

            if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(appName))
            {
                MessageBox.Show("Vui lòng điền đầy đủ tên Project và tên App!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string projectDir = Path.Combine(_projectsRoot, projectName);
                if (Directory.Exists(projectDir))
                {
                    MessageBox.Show("Thư mục project này đã tồn tại trên ổ đĩa!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Directory.CreateDirectory(projectDir);

                // 1. Tạo thư mục src hoặc để code trực tiếp trong root project
                string srcDir = Path.Combine(projectDir, "src");
                string resDir = Path.Combine(projectDir, "res");
                Directory.CreateDirectory(srcDir);
                Directory.CreateDirectory(resDir);

                // 2. Tạo file code mẫu main.py
                string mainFileName = string.IsNullOrEmpty(mainClassFull) ? "main.py" : (mainClassFull.EndsWith(".py") ? mainClassFull : mainClassFull + ".py");
                string mainFilePath = Path.Combine(srcDir, mainFileName);

                string pythonSampleCode =
                    $"# Project: {appName}\n" +
                    $"# Author: {author}\n\n" +
                    $"def main():\n" +
                    $"  print('Hello from Python Mini IDE!')\n\n" +
                    $"if __name__ == '__main__':\n" +
                    $"  main()\n";

                File.WriteAllText(mainFilePath, pythonSampleCode, new UTF8Encoding(false));

                // 3. Tạo file cấu hình python_config.json
                var configObj = new { AppName = appName, MainScript = mainFileName, Author = author };
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string jsonString = JsonSerializer.Serialize(configObj, options);
                File.WriteAllText(Path.Combine(projectDir, "python_config.json"), jsonString, new UTF8Encoding(false));

                CreatedProjectPath = projectDir;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tạo project: {ex.Message}", "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}