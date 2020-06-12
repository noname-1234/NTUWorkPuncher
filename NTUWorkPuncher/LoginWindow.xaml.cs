using System;
using System.Threading.Tasks;
using System.Windows;

namespace NTUWorkPuncher
{
    /// <summary>
    /// LoginWindow.xaml 的互動邏輯
    /// </summary>
    public partial class LoginWindow : Window
    {

        private Puncher puncher { get; set; }

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Account.Text = Properties.Settings.Default.Account;
            Password.Password = Properties.Settings.Default.Password;
        }

        public Puncher GetPuncher()
        {
            return puncher;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string account = Account.Text;
            string password = Password.Password;

            if (string.IsNullOrEmpty(account))
            {
                MessageBox.Show(this, "請填入 MyNTU 帳號", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show(this, "請填入 MyNTU 密碼", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BusyIndicator.IsBusy = true;
            (bool loginSuccess, string errMsg) = await Task.Run(() => login(account, password));
            BusyIndicator.IsBusy = false;

            if (!loginSuccess)
            {
                MessageBox.Show(this, errMsg, "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Properties.Settings.Default.Account = account;
            Properties.Settings.Default.Password = password;
            Properties.Settings.Default.Save();
            Close();
        }

        private (bool, string) login(string account, string password)
        {
            puncher = new Puncher(Account: account, Password: password);
            try
            {
                puncher.Login();
            }
            catch (AuthFailedException)
            {
                puncher = null;
                return (false, "登入失敗, 請確認帳號密碼是否正確");
            }
            catch (Exception ex)
            {
                puncher = null;
                return (false, $"登入時出現未知的錯誤: {ex.Message}");
            }

            // login success
            return (true, null);
        }

        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                LoginButton_Click(sender, e);
            }
        }
    }
}
