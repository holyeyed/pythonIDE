using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace PythonEditor
{
    public partial class InputNameDialog : Window
    {
        // Trả về chuỗi người dùng đã nhập
        public string InputText => txtInput.Text.Trim();

        public InputNameDialog(string windowTitle, string promptMessage, string defaultText = "")
        {
            InitializeComponent();
            this.Title = windowTitle;
            lblPrompt.Text = promptMessage;
            txtInput.Text = defaultText;

            // Bôi đen sẵn text để tiện gõ đè (rất hữu ích khi Rename)
            txtInput.SelectAll();
            txtInput.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtInput.Text))
            {
                MessageBox.Show("Tên không được để trống!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}