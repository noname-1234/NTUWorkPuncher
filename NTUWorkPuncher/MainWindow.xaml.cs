using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace NTUWorkPuncher
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {

        private Puncher puncher { get; set; }

        private NotifyIcon notifyIcon { get; set; }

        private CardRecordItem todayRecord { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            setTimer();
            setNotifyIcon();

            List<string> hours = Enumerable.Range(0, 24).Select(x => x.ToString("00")).ToList();
            List<string> mins = Enumerable.Range(0, 60).Select(x => x.ToString("00")).ToList();
            AutoPunchInH.ItemsSource = hours;
            AutoPunchInM.ItemsSource = mins;
            AutoPunchOutH.ItemsSource = hours;
            AutoPunchOutM.ItemsSource = mins;

            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            saveSettings();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            notifyIcon = null;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            setClock();
            restoreSettings();
        }

        private void MenuItemExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                setClock();
                DateTime currentTime = DateTime.Now;
                string timeOfDay = currentTime.TimeOfDay.ToString(@"hh\:mm\:ss");

                if (puncher == null)
                {
                    return;
                }

                if (timeOfDay == "00:00:00")
                {
                    fetchRecords();
                }

                if (AutoPunchIn.IsChecked.Value)
                {
                    if (timeOfDay != $"{AutoPunchInH.SelectedItem.ToString()}:{AutoPunchInM.SelectedItem.ToString()}:00")
                    {
                        goto AUTO_PUNCH_OUT;
                    }

                    if (puncher.IsTodayHoliday())
                    {
                        Debug.WriteLine("今日為例假日, 跳過自動打卡步驟");
                        return;
                    }

                    fetchRecords();
                    if (todayRecord != null && !string.IsNullOrEmpty(todayRecord.PunchedInString))
                    {
                        puncher.LineNotify($"今日已在{todayRecord.PunchedInString}打過上班卡, 故跳過自動打卡");
                        goto AUTO_PUNCH_OUT;
                    }

                    puncher.PunchIn();
                    fetchRecords();
                    puncher.LineNotify($"今日已成功在{todayRecord.PunchedInString}自動打上班卡");
                }

            AUTO_PUNCH_OUT:
                if (AutoPunchOut.IsChecked.Value)
                {
                    if (timeOfDay != $"{AutoPunchOutH.SelectedItem.ToString()}:{AutoPunchOutM.SelectedItem.ToString()}:00")
                    {
                        return;
                    }

                    if (puncher.IsTodayHoliday())
                    {
                        Debug.WriteLine("今日為例假日, 跳過自動打卡步驟");
                        return;
                    }

                    fetchRecords();
                    if (todayRecord == null || string.IsNullOrEmpty(todayRecord.PunchedInString))
                    {
                        puncher.LineNotify($"今日沒打上班卡, 因此忽略自動打下班卡步驟");
                        return;
                    }

                    puncher.PunchOut();
                    fetchRecords();
                    puncher.LineNotify($"今日已成功在{todayRecord.PunchedOutString}自動打下班卡");
                }
            }
            catch (Exception ex)
            {
                handleException(ex);
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoginWindow loginWindow = new LoginWindow()
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                loginWindow.ShowDialog();
                puncher = loginWindow.GetPuncher();
                if (puncher == null)
                {
                    return;
                }
                if (!string.IsNullOrEmpty(LineNotifyToken.Text))
                {
                    puncher.SetLineNotifyAPIToken(LineNotifyToken.Text);
                }
                setAfterLogined();
            }
            catch (Exception ex)
            {
                handleException(ex);
                Close();
            }
        }

        private void PunchInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                fetchRecords();
                if (todayRecord != null && !string.IsNullOrEmpty(todayRecord.PunchedInString))
                {
                    return;
                }
                puncher.PunchIn();
                fetchRecords();
            }
            catch (Exception ex)
            {
                handleException(ex);
            }
        }

        private void PunchOutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                fetchRecords();
                puncher.PunchOut();
                fetchRecords();
            }
            catch (Exception ex)
            {
                handleException(ex);
            }
        }

        private void AutoPunchIn_Checked(object sender, RoutedEventArgs e)
        {
            if (AutoPunchInH.SelectedIndex == -1)
            {
                AutoPunchInH.SelectedIndex = 0;
            }
            if (AutoPunchInM.SelectedIndex == -1)
            {
                AutoPunchInM.SelectedIndex = 0;
            }
        }

        private void AutoPunchOut_Checked(object sender, RoutedEventArgs e)
        {
            if (AutoPunchOutH.SelectedIndex == -1)
            {
                AutoPunchOutH.SelectedIndex = 0;
            }
            if (AutoPunchOutM.SelectedIndex == -1)
            {
                AutoPunchOutM.SelectedIndex = 0;
            }
        }

        private void LineNotify_Checked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LineNotifyToken.Text))
            {
                LineNotify.IsChecked = false;
                return;
            }
            else
            {
                LineNotifyToken.IsEnabled = false;
                if (puncher != null)
                {
                    puncher.SetLineNotifyAPIToken(LineNotifyToken.Text);
                }
            }
        }

        private void LineNotify_Unchecked(object sender, RoutedEventArgs e)
        {
            LineNotifyToken.IsEnabled = true;
            puncher.ClearLineNotifyAPIToken();
        }

        private void LineNotifyToken_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(LineNotifyToken.Text))
            {
                LineNotify.IsChecked = false;
                LineNotify.IsEnabled = false;
            }
            else
            {
                LineNotify.IsEnabled = true;
            }
        }

        private void NotifyIcon_DoubleClicked(object sender, EventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
        }

        private void restoreSettings()
        {
            AutoPunchIn.IsChecked = Properties.Settings.Default.AutoPunchIn;
            AutoPunchInH.SelectedIndex = Properties.Settings.Default.AutoPunchInH;
            AutoPunchInM.SelectedIndex = Properties.Settings.Default.AutoPunchInM;
            AutoPunchOut.IsChecked = Properties.Settings.Default.AutoPunchOut;
            AutoPunchOutH.SelectedIndex = Properties.Settings.Default.AutoPunchOutH;
            AutoPunchOutM.SelectedIndex = Properties.Settings.Default.AutoPunchOutM;
            LineNotifyToken.Text = Properties.Settings.Default.LineNotifyAPIToken;
            LineNotify.IsChecked = Properties.Settings.Default.LineNotify;
        }

        private void saveSettings()
        {
            Properties.Settings.Default.AutoPunchIn = AutoPunchIn.IsChecked.Value;
            Properties.Settings.Default.AutoPunchInH = AutoPunchInH.SelectedIndex;
            Properties.Settings.Default.AutoPunchInM = AutoPunchInM.SelectedIndex;
            Properties.Settings.Default.AutoPunchOut = AutoPunchOut.IsChecked.Value;
            Properties.Settings.Default.AutoPunchOutH = AutoPunchOutH.SelectedIndex;
            Properties.Settings.Default.AutoPunchOutM = AutoPunchOutM.SelectedIndex;
            Properties.Settings.Default.LineNotifyAPIToken = LineNotifyToken.Text;
            Properties.Settings.Default.LineNotify = LineNotify.IsChecked.Value;
            Properties.Settings.Default.Save();
        }

        private void setTimer()
        {
            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void setNotifyIcon()
        {
            System.Windows.Forms.ContextMenu notifyIconContextMenu = new System.Windows.Forms.ContextMenu();
            System.Windows.Forms.MenuItem menuItemExit = new System.Windows.Forms.MenuItem
            {
                Text = "關閉程式",
            };
            menuItemExit.Click += MenuItemExit_Click;
            notifyIconContextMenu.MenuItems.Add(menuItemExit);
            notifyIcon = new NotifyIcon
            {
                Icon = Properties.Resources.icon,
                Visible = true,
                Text = "NTU打卡小工具: (尚未登入)",
                ContextMenu = notifyIconContextMenu
            };
            notifyIcon.DoubleClick += NotifyIcon_DoubleClicked;
        }

        private bool checkPuncherNeedReLogin()
        {
            TimeSpan timeSpan = new TimeSpan(DateTime.Now.Ticks - puncher.TimeLogined.Ticks);
            return timeSpan.TotalMinutes > 15;
        }

        private void fetchRecords()
        {
            try
            {
                if (puncher == null)
                {
                    throw new NotLoginedException();
                }

                if (checkPuncherNeedReLogin())
                {
                    puncher.Login();
                }

                CardRecord records = puncher.FetchRecords();
                if (records.Items.Count > 0)
                {
                    var itemSource = records.Items.Select(x => new { 日期 = x.SignDateString, 簽到時間 = x.PunchedInString, 簽退時間 = x.PunchedOutString });
                    Card.ItemsSource = itemSource;
                    Card.Columns[0].Width = 120;
                    Card.Columns[1].Width = 180;
                    Card.Columns[2].Width = 180;
                }

                todayRecord = null;
                PunchInButton.IsEnabled = true;
                PunchedIn.Content = string.Empty;
                PunchedOut.Content = string.Empty;

                IEnumerable<CardRecordItem> todayRecordQuery = records.Items.Where(x => x.PunchedIn.Value.Date == DateTime.Today.Date);
                if (!todayRecordQuery.Any())
                {
                    return;
                }

                todayRecord = todayRecordQuery.First();
                PunchedIn.Content = todayRecord.PunchedInString;
                if (!string.IsNullOrEmpty(todayRecord.PunchedOutString))
                {
                    PunchedOut.Content = todayRecord.PunchedOutString;
                }
                PunchInButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                throw new FetchCardsFailed("取得卡時失敗", ex);
            }
        }

        private void setClock()
        {
            Clock.Content = DateTime.Now.ToString("hh : mm : ss");
        }

        private void setAfterLogined()
        {
            Height = 650;
            PanelBeforeLogin.Visibility = Visibility.Hidden;
            PanelAfterLogin.Visibility = Visibility.Visible;

            Username.Content = $"{puncher.Username}您好:";
            notifyIcon.Text = $"NTU打卡小工具: {puncher.Username}";
            Date.Content = $"今天是{DateTime.Today.ToString("yyyy-MM-dd")}";

            fetchRecords();
        }

        private void handleException(Exception exception)
        {
            if (exception is AuthFailedException)
            {
                sendAlert($"登入失敗");
            }
            else if (exception is EmptyResponseException)
            {
                sendAlert("WebAPI 得到空回應");
            }
            else if (exception is APIFailedException)
            {
                sendAlert("WebAPI 失敗");
            }
            else if (exception is NotLoginedException)
            {
                sendAlert("尚未登入的狀態, 請確認是否正常登入");
            }
            else if (exception is FetchCardsFailed)
            {
                sendAlert($"{exception.Message}:{exception.InnerException.Message}");
            }
            else
            {
                sendAlert($"執行發生例外: {exception.Message}");
            }
        }

        private void sendAlert(string msg)
        {
            if (LineNotify.IsChecked.Value)
            {
                puncher.LineNotify(msg);
            }
            System.Windows.MessageBox.Show(msg, "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
