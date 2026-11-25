using ICSharpCode.AvalonEdit.Highlighting;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;

namespace PythonEditor
{
    public partial class MainWindow : Window
    {
        string ini = "python_config_path.ini";
        string rootProject = string.Empty;
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        char sepDir=System.IO.Path.DirectorySeparatorChar;
        private string _projectDir = string.Empty;
        private string _currentOpenedFile = string.Empty;
        Boolean pmode = true;
        Encoding utf8WithoutBom = new UTF8Encoding(false);
        private bool _canSendInput = false;
        private Process? _runningProcess;
        private bool _isStoppedByUser = false;
        private int _inputStartOffset = 0;
        public MainWindow()
        {
            InitializeComponent();
            InitPythonIntelliSense();
            rootProject = Path.Combine(exeDir, "projects");
            if (!Directory.Exists(rootProject)) Directory.CreateDirectory(rootProject);
            if (File.Exists(exeDir + sepDir + ini))
            {
                string path = File.ReadAllText(exeDir + sepDir + ini, Encoding.UTF8);
                if (Directory.Exists(path))
                {
                    _projectDir = path;
                }
            }
            ApplyVsCodeDarkThemeForPython();
            if (Directory.Exists(_projectDir)) {
                LoadProjectTree(_projectDir);
                LoadProjectConfig();
                pmode=false;
            }
            else
            {
               ScanProjects();
            }

            tvProject.MouseDoubleClick += TvProject_MouseDoubleClick;
        }

        private void InitPythonIntelliSense()
        {
            // Lắng nghe sự kiện gợi ý code nhẹ nhàng cho từ khóa Python
            codeEditor.TextArea.TextEntered += CodeEditor_TextEntered;
        }

        private void CodeEditor_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && (char.IsLetter(e.Text[0]) || e.Text[0] == '.'))
            {
                // Có thể mở rộng gợi ý từ khóa Python ở đây nếu muốn
            }
        }

        private void ApplyVsCodeDarkThemeForPython()
        {
            // Thiết lập Syntax Highlighting có sẵn chuẩn Python của AvalonEdit
            var pythonHighlighting = HighlightingManager.Instance.GetDefinition("Python");
            if (pythonHighlighting != null)
            {
                codeEditor.SyntaxHighlighting = pythonHighlighting;
            }
        }

        public void ScanProjects()
        {
            tvProject.Items.Clear();

            foreach (var dir in Directory.GetDirectories(rootProject))
            {
                string configPath = Path.Combine(dir, "python_config.json");
                if (File.Exists(configPath))
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(configPath, new UTF8Encoding(false));
                        using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                        {
                            JsonElement root = doc.RootElement;
                            string appName = root.TryGetProperty("AppName", out JsonElement el) ? el.GetString() : Path.GetFileName(dir);

                            var node = new ProjectNodeModel
                            {
                                Name = appName ?? Path.GetFileName(dir),
                                FullPath = dir,
                                IsDirectory = true,
                                IsRoot = true
                            };
                            tvProject.Items.Add(node);
                        }
                    }
                    catch { }
                }
            }
            pmode = true;
        }

        private void LoadProjectTree(string rootFolderPath)
        {
            tvProject.Items.Clear();
            if (!Directory.Exists(rootFolderPath)) return;
            //AppendLog(rootFolderPath);
            var rootItem = new ProjectNodeModel
            {
                Name = Path.GetFileName(rootFolderPath),
                FullPath = rootFolderPath,
                IsDirectory = true,
                IsRoot = true
            };

            LoadDirectoryRecursive(rootFolderPath, rootItem);
            tvProject.Items.Add(rootItem);
            File.WriteAllText(exeDir + sepDir + ini, rootFolderPath, utf8WithoutBom);
        }

        private void LoadDirectoryRecursive(string dirPath, ProjectNodeModel parentItem)
        {
            try
            {
                foreach (string subDir in Directory.GetDirectories(dirPath))
                {
                    var dirInfo = new DirectoryInfo(subDir);
                    if (dirInfo.Name == "__pycache__" || dirInfo.Name == ".git" || dirInfo.Name == "build") continue;

                    var item = new ProjectNodeModel
                    {
                        Name = dirInfo.Name,
                        FullPath = subDir,
                        IsDirectory = true
                    };
                    parentItem.Children.Add(item);
                    LoadDirectoryRecursive(subDir, item);
                }

                foreach (string filePath in Directory.GetFiles(dirPath))
                {
                    var fileInfo = new FileInfo(filePath);
                    var item = new ProjectNodeModel
                    {
                        Name = fileInfo.Name,
                        FullPath = filePath,
                        IsDirectory = false
                    };
                    parentItem.Children.Add(item);
                }
            }
            catch { }
        }

        private void TvProject_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (tvProject.SelectedItem is ProjectNodeModel selectedItem)
            {
                if (selectedItem.FullPath is string filePath)
                {
                    // Cho phép mở file .py, .txt, .json, .md
                    if (File.Exists(filePath) && (filePath.EndsWith(".py") || filePath.EndsWith(".txt") || filePath.EndsWith(".json") || filePath.EndsWith(".md")))
                    {
                        SaveCurrentFile();
                        _currentOpenedFile =filePath;
                        try
                        {
                            codeEditor.Text = File.ReadAllText(filePath, utf8WithoutBom);
                            txtCurrentFileName.Text = Path.GetFileName(_currentOpenedFile);
                            txtCurrentFileName.Foreground = Brushes.White;
                            txtCurrentFileName.FontStyle = FontStyles.Normal;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Không thể đọc file: {ex.Message}");
                        }
                        return;
                    }
                }

                if (pmode && selectedItem.IsRoot)
                {
                    _projectDir = selectedItem.FullPath;
                    LoadProjectTree(_projectDir);
                    LoadProjectConfig();
                    pmode = false;
                }
            }
        }

        private void BtnNewProject_Click(object sender, RoutedEventArgs e)
        {
            var newwin = new NewProjectWindow(rootProject);
            newwin.Owner = this;
            if (newwin.ShowDialog() == true)
            {
                _projectDir = newwin.CreatedProjectPath;
                LoadProjectTree(_projectDir);
                LoadProjectConfig();
                pmode = false;
            }
        }

        private void BtnCloseProject_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentFile();
            CloseCurrentFile();
            txtAppName.Text = "";
            txtMainClass.Text = "";
            txtAuthor.Text = "";
            ScanProjects();
            codeEditor.Text = "# Project closed\n# Select a project to open...";
        }

        private void CloseCurrentFile()
        {
            codeEditor.Clear();
            txtCurrentFileName.Text = "No file opened";
            _currentOpenedFile = string.Empty;
            txtCurrentFileName.Foreground = (Brush)new BrushConverter().ConvertFrom("#858585");
            txtCurrentFileName.FontStyle = FontStyles.Italic;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentFile();
        }

        private void SaveCurrentFile()
        {
            if (string.IsNullOrEmpty(_currentOpenedFile)) return;
            try
            {
                File.WriteAllText(_currentOpenedFile, codeEditor.Text, utf8WithoutBom);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu file: {ex.Message}");
            }
        }

        // --- TÍNH NĂNG RUN PYTHON SCRIPT THAY THẾ CHO JAVAC/PROGUARD ---
        private async void BtnBuildRun_Click(object sender, RoutedEventArgs e)
        {
            if (pmode) { MessageBox.Show("Vui lòng mở một Project trước!"); return; }

            SaveCurrentFile(); // Tự động lưu file hiện tại trước khi chạy

            // Xác định file cần chạy: Ưu tiên file đang mở nếu là file .py, ngược lại tìm file đầu tiên trong src hoặc root
            string targetScript = _currentOpenedFile;
            if (string.IsNullOrEmpty(targetScript) || !targetScript.EndsWith(".py"))
            {
                MessageBox.Show("Hãy mở và chọn một file .py để chạy!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            txtOutput.Clear();
            txtOutput.AppendText($"[Running] python \"{(targetScript)}\"...\n\n");
            await RunPythonScriptAsync("python", targetScript, _projectDir);
            
        }


        private async Task RunPythonScriptAsync(string pythonExe, string scriptPath, string workingDir)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                // Thêm -u để Python tắt buffer stdout/stderr, giúp dòng prompt như input("...") hiện sớm hơn
                Arguments = $"-u \"{scriptPath}\"",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Ép Python dùng UTF-8 cho stdout/stderr/stdin khi chạy qua pipe
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

            StartInputMode();

            try
            {
                using var process = new Process { StartInfo = startInfo };
                _runningProcess = process;
                SetProcessButtonsState(true);
                process.Start();
                var stdoutTask = ReadStreamRealtimeAsync(process.StandardOutput.BaseStream, Encoding.UTF8);
                var stderrTask = ReadStreamRealtimeAsync(process.StandardError.BaseStream, Encoding.UTF8);

                await Task.Run(() => process.WaitForExit());
                await Task.WhenAll(stdoutTask, stderrTask);

                if (_isStoppedByUser)
                    AppendRawText($"{Environment.NewLine}[Stopped] Đã dừng tiến trình theo yêu cầu.{Environment.NewLine}");
                else
                    AppendRawText($"{Environment.NewLine}[Done] ExitCode: {process.ExitCode}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                AppendLog($"\n[ERROR] {ex.Message}");
            }
            finally
            {
                _runningProcess = null;
                StopInputMode() ;
                SetProcessButtonsState(false);
            }
        }

        private async Task ReadStreamRealtimeAsync(Stream stream, Encoding encoding)
        {
            using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            char[] buffer = new char[256];

            while (true)
            {
                int charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (charsRead <= 0)
                    break;

                string chunk = new string(buffer, 0, charsRead);
                AppendRawText(chunk);
            }
        }


        private void AppendRawText(string text)
        {
            Dispatcher.Invoke(() =>
            {
                string normalized = text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
                txtOutput.AppendText(normalized);
                txtOutput.ScrollToEnd();

                // Cập nhật mốc bắt đầu phần input mới
                _inputStartOffset = txtOutput.Text.Length;
                txtOutput.CaretIndex = _inputStartOffset;
            });
        }
        // Bật khi tiến trình bắt đầu chạy
        private void StartInputMode()
        {
            Dispatcher.Invoke(() =>
            {
                _canSendInput = true;
                txtOutput.IsReadOnly = false;
                txtOutput.CaretIndex = txtOutput.Text.Length;
                _inputStartOffset = txtOutput.Text.Length;
            });
        }

        // Tắt khi tiến trình kết thúc
        private void StopInputMode()
        {
            Dispatcher.Invoke(() =>
            {
                _canSendInput = false;
                txtOutput.IsReadOnly = true;
            });
        }

        private async void TxtOutput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_canSendInput || _runningProcess == null || _runningProcess.HasExited)
                return;

            // Không cho Backspace xóa lẹm qua phần gợi ý / log phía trên
            if (e.Key == Key.Back && txtOutput.SelectionLength == 0 && txtOutput.CaretIndex <= _inputStartOffset)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                int start = Math.Clamp(_inputStartOffset, 0, txtOutput.Text.Length);
                string userInput = txtOutput.Text.Substring(start);

                txtOutput.AppendText(Environment.NewLine);
                txtOutput.ScrollToEnd();
                _inputStartOffset = txtOutput.Text.Length;
                txtOutput.CaretIndex = _inputStartOffset;

                try
                {
                    await _runningProcess.StandardInput.WriteLineAsync(userInput);
                    await _runningProcess.StandardInput.FlushAsync();
                }
                catch (Exception ex)
                {
                    AppendRawText($"[STDIN ERROR] {ex.Message}{Environment.NewLine}");
                }
            }
        }


        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopRunningProcess();
        }

        private void StopRunningProcess()
        {
            try
            {
                if (_runningProcess != null && !_runningProcess.HasExited)
                {
                    _isStoppedByUser = true;
                    _runningProcess.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                AppendRawText($"{Environment.NewLine}[STOP WARNING] {ex.Message}{Environment.NewLine}");
            }
        }

        private void SetProcessButtonsState(bool isRunning)
        {
            Dispatcher.Invoke(() =>
            {
                // Giả sử nút chạy của bác tên là btnRun hoặc btnBuildRun
                btnBuildRun.IsEnabled = !isRunning;
                btnStop.IsEnabled = isRunning;
            });
        }
        private void BtnPipInstall_Click(object sender, RoutedEventArgs e)
        {
            // Sau này bác có biến lưu đường dẫn python.exe / venv thì truyền vào đây luôn
            var dialog = new PipInstallDialog("python")
            {
                Owner = this
            };
            dialog.Show();
        }

        // Thụt lề 4 dấu cách cho các dòng đang chọn (hoặc dòng hiện tại)
        private void MenuFormat_Indent_Click(object sender, RoutedEventArgs e)
        {
            TransformSelectedLines(line => "    " + line);
        }

        // Bỏ bớt tối đa 4 dấu cách (hoặc 1 tab) đầu mỗi dòng
        private void MenuFormat_Unindent_Click(object sender, RoutedEventArgs e)
        {
            TransformSelectedLines(line =>
            {
                if (line.StartsWith("    ")) return line.Substring(4);
                if (line.StartsWith("\t")) return line.Substring(1);
                return line.TrimStart(' ');
            });
        }

        // Đổi Tab thành 4 spaces toàn văn bản
        private void MenuFormat_TabsToSpaces_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(codeEditor.Text)) return;
            int caret = codeEditor.CaretOffset;
            codeEditor.Text = codeEditor.Text.Replace("\t", "    ");
            codeEditor.CaretOffset = Math.Min(caret, codeEditor.Text.Length);
        }

        // Xóa khoảng trắng dư ở cuối từng dòng
        private void MenuFormat_TrimTrailing_Click(object sender, RoutedEventArgs e)
        {
            var doc = codeEditor.Document;
            if (doc == null || doc.LineCount == 0) return;

            using (doc.RunUpdate())
            {
                for (int i = 1; i <= doc.LineCount; i++)
                {
                    var line = doc.GetLineByNumber(i);
                    string raw = doc.GetText(line);
                    string trimmed = raw.TrimEnd(' ', '\t');
                    if (trimmed.Length != raw.Length)
                    {
                        doc.Replace(line.Offset, line.Length, trimmed);
                    }
                }
            }
        }

        // Bật/tắt comment "# " cho khối dòng đang chọn
        private void MenuFormat_ToggleComment_Click(object sender, RoutedEventArgs e)
        {
            TransformSelectedLines(line =>
            {
                string trimmedStart = line.TrimStart(' ');
                int indentLen = line.Length - trimmedStart.Length;
                string indent = line.Substring(0, indentLen);

                if (trimmedStart.StartsWith("# "))
                    return indent + trimmedStart.Substring(2);
                if (trimmedStart.StartsWith("#"))
                    return indent + trimmedStart.Substring(1);

                return indent + "# " + trimmedStart;
            });
        }

        private void TransformSelectedLines(Func<string, string> lineTransform)
        {
            var doc = codeEditor.Document;
            if (doc == null) return;

            int startLineNum = doc.GetLineByOffset(codeEditor.SelectionStart).LineNumber;
            int endOffset = codeEditor.SelectionStart + codeEditor.SelectionLength;
            int endLineNum = doc.GetLineByOffset(Math.Min(endOffset, doc.TextLength)).LineNumber;

            // Nếu bôi đen kết thúc ngay đầu dòng tiếp theo (không chọn ký tự nào trên dòng đó), lùi lại 1 dòng
            if (codeEditor.SelectionLength > 0 && endLineNum > startLineNum)
            {
                var lastLine = doc.GetLineByNumber(endLineNum);
                if (endOffset == lastLine.Offset)
                    endLineNum--;
            }

            using (doc.RunUpdate())
            {
                for (int num = startLineNum; num <= endLineNum; num++)
                {
                    var line = doc.GetLineByNumber(num);
                    string oldText = doc.GetText(line);
                    string newText = lineTransform(oldText);
                    if (newText != oldText)
                    {
                        doc.Replace(line.Offset, line.Length, newText);
                    }
                }
            }
        }
        private void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText(message + "\r\n");
                txtOutput.ScrollToEnd();
            });
        }

        private string GetConfigFilePath()
        {
            if (string.IsNullOrEmpty(_projectDir)) return string.Empty;
            return Path.Combine(_projectDir, "python_config.json");
        }

        private void LoadProjectConfig()
        {
            string configFile = GetConfigFilePath();
            if (File.Exists(configFile))
            {
                try
                {
                    string jsonString = File.ReadAllText(configFile, new UTF8Encoding(false));
                    using (JsonDocument doc = JsonDocument.Parse(jsonString))
                    {
                        var root = doc.RootElement;
                        txtAppName.Text = root.TryGetProperty("AppName", out var v1) ? v1.GetString() : "";
                        txtMainClass.Text = root.TryGetProperty("MainScript", out var v2) ? v2.GetString() : "";
                        txtAuthor.Text = root.TryGetProperty("Author", out var v3) ? v3.GetString() : "";
                        return;
                    }
                }
                catch { }
            }
            txtAppName.Text = "";
            txtMainClass.Text = "";
            txtAuthor.Text = "";
        }

        // Các hàm Menu Context (New File, Rename, Delete) giữ nguyên như cũ nhưng trỏ về đuôi .py
        private void Menu_NewFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ProjectNodeModel targetNode)
            {
                if (!targetNode.IsDirectory) return;
                var dialog = new InputNameDialog("Tạo file Python mới", "Nhập tên file (VD: script.py):", "script.py");
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    string fullPath = Path.Combine(targetNode.FullPath, dialog.InputText);
                    File.WriteAllText(fullPath, "# -*- coding: utf-8 -*-\n\n", utf8WithoutBom);
                    LoadProjectTree(_projectDir);
                }
            }
        }

        private void Menu_Rename_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ProjectNodeModel targetNode)
            {
                var dialog = new InputNameDialog("Đổi tên", $"Đổi tên '{targetNode.Name}':", targetNode.Name);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    string parentDir = Path.GetDirectoryName(targetNode.FullPath);
                    string newFullPath = Path.Combine(parentDir, dialog.InputText);
                    if (targetNode.IsDirectory) Directory.Move(targetNode.FullPath, newFullPath);
                    else File.Move(targetNode.FullPath, newFullPath);
                    LoadProjectTree(_projectDir);
                }
            }
        }

        private void Menu_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ProjectNodeModel targetNode)
            {
                if (MessageBox.Show($"Xóa '{targetNode.Name}'?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    if (targetNode.IsDirectory) Directory.Delete(targetNode.FullPath, true);
                    else File.Delete(targetNode.FullPath);
                    LoadProjectTree(_projectDir);
                }
            }
        }
    }
}