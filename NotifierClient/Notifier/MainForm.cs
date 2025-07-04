using System;
using System.Windows.Forms;
using System.Globalization;
using System.Windows.Threading;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using System.Linq;
using Newtonsoft.Json;
using System.Threading;
using System.Text.RegularExpressions;
using System.Configuration;
using WebSocketSharp; // https://github.com/sta/websocket-sharp
using System.Windows.Media;
using System.Collections.Generic;
using System.Drawing;
using Color = System.Drawing.Color;

namespace Notifier
{
    public partial class MainForm : Form
    {
        // Disable the Close (x) button:
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle = cp.ClassStyle | 0x200; // CP_NOCLOSE_BUTTON
                return cp;
            }
        }

        MediaPlayer player = new MediaPlayer();

        const int timeout_sec = 10;
        const int last_sent_timeout = 1;
        readonly TimeSpan notify_timeout = TimeSpan.FromSeconds(30);


        static string my_connection_id = null;

        private DispatcherTimer Check_Input_Process_Timer;
        private DispatcherTimer Notify_Timer;
        private DispatcherTimer Display_Update_Timer;

        private KeyboardInput keyboard;
        private MouseInput mouse;

        private static string my_login_name;
        private string to_whom_to_send;
        private static string server_host_and_port;

        static private SynchronizationContext _uiSyncContext;
        static TextBox _StatusTextBox;
        static TextBox _recievedFromTextBox;
        static TextBox _lastActionBeforeTextBox;
        static TextBox _currentAppUsedTextBox;
        static TextBox _byInputUsedTextBox;
        static TextBox _currentAppUsedTitleTextBox;
        static TextBox _traceeStatusTextBox;

        public MainForm()
        {
            InitializeComponent();

            _uiSyncContext = SynchronizationContext.Current;
            _StatusTextBox = statusTextBox;
            _recievedFromTextBox = recievedFromTextBox;
            _lastActionBeforeTextBox = lastActionBeforeTextBox;
            _currentAppUsedTextBox = currentAppUsedTextBox;
            _byInputUsedTextBox = byInputUsedTextBox;
            _currentAppUsedTitleTextBox = currentAppUsedTitleTextBox;
            _traceeStatusTextBox = traceeStatusTextBox;

            // Load the settings
            HideOnStartCheckBox.Checked = Properties.Settings.Default.HideOnStart;
            AlwaysOnTopCheckBox.Checked = Properties.Settings.Default.AlwaysOnTop;
            SendDataCheckBox.Checked = Properties.Settings.Default.SendData;
            SendAlsoAppsDataCheckBox.Checked = Properties.Settings.Default.SendAlsoAppsData;
            MyLoginNameTextBox.Text = my_login_name = Properties.Settings.Default.MyLoginName;
            ToWhomToSendTextBox.Text = to_whom_to_send = Properties.Settings.Default.ToWhomToSend;
            Text += $" - {my_login_name} to {to_whom_to_send}";
            ServerHostAndPortTextBox.Text = server_host_and_port = ConfigurationManager.AppSettings["ServerHostAndPort"];

            //goto SKEEP_INPUT;

            Check_Input_Process_Timer = new DispatcherTimer();
            Check_Input_Process_Timer.Interval = new TimeSpan(0, 0, 0, 0, 200); // 200 ms
            Check_Input_Process_Timer.Tick += Handle_App_Process_Tick;
            Check_Input_Process_Timer.Start();

            Notify_Timer = new DispatcherTimer();
            Notify_Timer.Interval = new TimeSpan(0, 0, 0, 0, 500); // 500 ms
            Notify_Timer.Tick += Notify_Timer_Tick;
            Notify_Timer.Start();

            Display_Update_Timer = new DispatcherTimer();
            Display_Update_Timer.Interval = new TimeSpan(0, 0, 0, 0, 200); // 500 ms
            Display_Update_Timer.Tick += Display_Update_Process_Tick;
            Display_Update_Timer.Start();

            keyboard = new KeyboardInput();
            keyboard.KeyBoardKeyPressed += KeyBoardKeyPressed_event;

            mouse = new MouseInput();
            mouse.MouseMoved += MouseMoved_event;

            ShowNotification(to_whom_to_send); // Unknown

            InitializeWebSocketAsync(); // lilo3: should I place it upper?
        }

        static private WebSocket wss;
        DateTime Last_input_time;
        DateTime? lastRemoteUpdateTime = null; // lilo: do I need it?
        TimeSpan? lastUpdateShiftTime = null;
        DateTime? appUsedSince = null;
        string last_app_title = null;

        private void InitializeWebSocketAsync()
        {
            _uiSyncContext.Post(state =>
            {
                _StatusTextBox.Text = "Connecting...";
            }, null);

            var AppName = "Notify";
            var wss_url = $"wss://{server_host_and_port}/{AppName}";
            wss = new WebSocket(wss_url);

            wss.OnOpen += (sender, e) =>
            {
                write_line_to_debug("Connected to the WebSocket server: " + wss_url);
            };

            wss.OnMessage += (sender, e) =>
            {
                if (!e.IsText) throw new Exception("WSS expect only Text messages.");
                // Console.WriteLine($"Received message: {e.Data}");
                dynamic message = JsonConvert.DeserializeObject(e.Data);
                switch (message.op.Value)
                {
                    case "ws_connected":

                        // React:
                        //this.app.setState({
                        //    loading: true,
                        //    browser_id: json.browser_id,
                        //    status: "Connected",
                        //});
                        //this.app.do_on_connect(json.browser_id);

                        _uiSyncContext.Post(state =>
                        {
                            _StatusTextBox.Text = "Connected";
                        }, null);


                        my_connection_id = message.client_id;

                        var json = new
                        {
                            op = "client_login_name",
                            connection_id = my_connection_id, // just for debug.
                            client_login_name = my_login_name,
                            to_whom_to_send = to_whom_to_send
                        };

                        string json_str = JsonConvert.SerializeObject(json);
                        wss.Send(json_str);

                        break;

                    case "client_info":
                        //this.Invoke(new Action(() => {
                        //    _traceeStatusTextBox.Text = "Online";
                        //}));

                        DateTime Last_keyboard_time = message.Last_keyboard_time.ToObject<DateTime>();
                        DateTime Last_mouse_time = message.Last_mouse_time.ToObject<DateTime>();

                        if (Last_mouse_time == DateTime.MinValue && Last_keyboard_time == DateTime.MinValue)
                        {
                            this.Invoke(new Action(() => {
                                _recievedFromTextBox.Text = message.from_whom;
                                _currentAppUsedTextBox.Text = message.App;
                                _currentAppUsedTitleTextBox.Text = message.Title;
                                //if (!(message.is_tracer_connected?.Value ?? true)) // (message.is_tracer_connected != null && !message.is_tracer_connected?.Value)
                                //{
                                //    _traceeStatusTextBox.Text = "Offline";
                                //}
                            }));
                            return;
                        }

                        DateTime Current_time = message.Current_time.ToObject<DateTime>();
                        int result = DateTime.Compare(Last_keyboard_time, Last_mouse_time);
                        string last_input_type;
                        if (result < 0)
                        {
                            Last_input_time = Last_mouse_time;
                            last_input_type = "Mouse";
                        }
                        else
                        {
                            Last_input_time = Last_keyboard_time;
                            last_input_type = "Keyboard";
                        }
                        TimeSpan span_time = Current_time - Last_input_time;
                        string span_time_str;
                        if (span_time.TotalDays > 365)
                            span_time_str = "Unknown";
                        else
                            span_time_str = span_time.ToString("hh\\:mm\\:ss");

                        Console.WriteLine($"Tracer got client_info: used input {span_time_str} sec. ago, by: {last_input_type}, App: {message.App}, Title: {message.Title}");

                        const string pattern = @"\s?\(.*?\)\s?";
                        string app_title = Regex.Replace(message.App.Value, pattern, "") + "|" + Regex.Replace(message.Title.Value, pattern, "");
                        if (last_app_title != app_title)
                        {
                            this.Invoke(new Action(() => {
                                if (notifyWhenOverCheckBox.Checked)
                                {
                                    this.Invoke(new Action(() => {
                                        player.Open(new Uri(AppDomain.CurrentDomain.BaseDirectory + @"sounds/DoneApp.mp3"));
                                        player.Play();
                                    }));
                                }
                                if (last_app_title == null)
                                {
                                    appUsedSince = Last_input_time;
                                }
                                else
                                {
                                    appUsedSince = DateTime.Now;
                                }
                                last_app_title = app_title;
                                notifyWhenOverCheckBox.Checked = false;
                            }));
                        }

                        lastRemoteUpdateTime = Current_time; // lilo: needed?
                        lastUpdateShiftTime = Current_time - Last_input_time; // lilo: DateTime.Now?


                        this.Invoke(new Action(() => {
                            _recievedFromTextBox.Text = message.from_whom;
                            _lastActionBeforeTextBox.Text = span_time_str;
                            _byInputUsedTextBox.Text = last_input_type;
                            _currentAppUsedTextBox.Text = message.App;
                            _currentAppUsedTitleTextBox.Text = message.Title;
                            if (!(message.is_tracee_connected?.Value ?? true)) // (message.is_tracer_connected != null && !message.is_tracer_connected?.Value)
                            {
                                _traceeStatusTextBox.BackColor = Control.DefaultBackColor;
                                _traceeStatusTextBox.Text = "Offline";
                                notifyWhenActiveCheckBox.Checked = true;
                                ShowNotification(to_whom_to_send);
                            }
                            //else if (_notifyWhenOnlineCheckBox.Checked && message.is_tracee_connected?.Value ?? false)
                            //{
                            //    player.Position = TimeSpan.Zero;
                            //    player.Play();
                            //}
                            //_notifyWhenOnlineCheckBox.Enabled = span_time > notify_timeout || _traceeStatusTextBox.Text != "Online";
                            //_notifyWhenOnlineCheckBox.Checked = false;
                        }));

                        break;

                    case "tracee_connected":
                        this.Invoke(new Action(() => {
                            _recievedFromTextBox.Text = message.tracee_login + " (tracee_connected)";
                            _traceeStatusTextBox.BackColor = System.Drawing.Color.LightGreen;
                            _traceeStatusTextBox.Text = "Online";
                            ShowNotification(message.tracee_login.Value);
                            notifyWhenOfflineCheckBox.Enabled = true;
                            //if (_notifyWhenOnlineCheckBox.Checked)
                            //{
                            //    player.Position = TimeSpan.Zero;
                            //    player.Play();
                            //}
                            //_notifyWhenOnlineCheckBox.Checked = false;
                            //_notifyWhenOnlineCheckBox.Enabled = false;
                        }));
                        break;

                    case "tracee_disconnected":
                        //if (message.tracee_login.Value != MyLoginNameTextBox.Text)
                        //{
                        //    throw new Exception();
                        //}

                        this.Invoke(new Action(() => {
                            _traceeStatusTextBox.BackColor = Control.DefaultBackColor;
                            _traceeStatusTextBox.Text = "Offline";
                            if (notifyWhenOfflineCheckBox.Checked)
                            {
                                player.Open(new Uri(AppDomain.CurrentDomain.BaseDirectory + @"sounds/Offline.mp3"));
                                player.Play();
                                ShowNotification(to_whom_to_send);
                            }
                            notifyWhenOfflineCheckBox.Checked = false;
                            notifyWhenOfflineCheckBox.Enabled = false;
                            notifyWhenActiveCheckBox.Checked = true; // lilo
                            notifyWhenActiveCheckBox.Enabled = true;
                            notifyWhenInactiveCheckBox.Checked = false;
                        }));
                        break;

                    case "show_custom_popup":
                        List<string> choicesList = ((IEnumerable<dynamic>)message.choices_list).Select(x => (string)x).ToList();

                        runCustomPopup((string)message.popup_form_title, (string)message.popup_message_title, choicesList);
                        break;

                    case "custom_popup_response":
                        this.Invoke(new Action(() =>
                        {
                            popupResponseTextBox.Text = (string)message.dialog_result;
                            popupRecievedTime = DateTime.Now;
                            popupRecievedTimeTextBox.Text = popupRecievedTime.ToString("HH\\:mm\\:ss");
                            closePopupButton.Enabled = false;
                            sendPopupButton.Enabled = true;
                        }));
                        break;

                    case "close_custom_popup":
                        this.Invoke(new Action(() =>
                        {
                            if (customPopup?.IsDisposed ?? true)
                            {
                                return;
                                MessageBox.Show("Popup is not shown");
                            }

                            customPopup.popup_status = "closed by operator";
                            customPopup.closeCustomPopup();
                        }));
                        break;

                    default:
                        throw new Exception("Unhandled message.op: " + message.op);
                }
            };

            wss.OnError += (sender, e) =>
            {
                Console.WriteLine($"WebSocket error: {e.Message}");
                // lilo: Is OnClose also called?
            };

            wss.OnClose += (sender, e) =>
            {
                _uiSyncContext.Post(state =>
                {
                    if (e.Code == 4001)
                    {
                        _StatusTextBox.Text = e.Reason;
                    }
                    else
                    {
                        _StatusTextBox.Text = "Disconnected";
                    }
                    _traceeStatusTextBox.BackColor = Control.DefaultBackColor;
                    _traceeStatusTextBox.Text = "Unknown";
                    notifyWhenActiveCheckBox.Enabled = true;
                    notifyWhenActiveCheckBox.Checked = true;
                    notifyWhenInactiveCheckBox.Checked = false;
                }, null);

                handle_reconnect();
            };

            wss.Connect();
        }

        void handle_reconnect()
        {
            Console.WriteLine($"Reconnecting in {timeout_sec} sec.");

            System.Threading.Timer timer = null;
            timer = new System.Threading.Timer((state) =>
            {
                InitializeWebSocketAsync();
                timer.Dispose();
            }, null, 10000, Timeout.Infinite);
        }

        private string FormatDateTime(DateTime dateTime)
        {
            return dateTime.ToString("HH:mm:ss fff", CultureInfo.CurrentUICulture);
        }

        DateTime LastKeyborad_Time;
        DateTime LastMouse_Time;
        DateTime LastApp_Time;

        void KeyBoardKeyPressed_event(object sender, EventArgs e)
        {
            LastKeyborad_Time = DateTime.Now;
            LastKeyboradTextBox.Text = FormatDateTime(LastKeyborad_Time);
        }
        void MouseMoved_event(object sender, EventArgs e)
        {
            LastMouse_Time = DateTime.Now;
            bool debug = false;
            if (debug)
            {
                LastMouseTextBox.Text = FormatDateTime(LastMouse_Time);
            }
        }

        private static int LastIndexOfRegEx(string input, string pattern)
        {
            if (input == null) return -1;
            Regex regex = new Regex(pattern);
            MatchCollection matches = regex.Matches(input);
            if (matches.Count == 0) return -1;
            return matches[matches.Count - 1].Index;
        }

        ActiveWindow lastAppProcess = null;
        Hashtable chunk_ht = new Hashtable();

        class App_Cunk
        {
            public DateTime last_time;
            public TimeSpan total_time;
        }


        void Notify_Timer_Tick(object sender, EventArgs e) // Every 500 ms.
        {
            Info_Notify(true);
        }

        // updates chunk_ht
        void Handle_App_Process_Tick(object sender, EventArgs e) // Every 200 ms.
        {
            ActiveWindow currentAppProcess = getCurrentAppDetails();
            string key = currentAppProcess._AppName + "|" + currentAppProcess._Title;

            ActiveWindowProcessNameTextBox.Text = currentAppProcess.ProcessName;
            ActiveWindowAppNameTextBox.Text = currentAppProcess.AppName;
            ActiveWindowTitleTextBox.Text = currentAppProcess.Title;
            _AppNameTextBox.Text = currentAppProcess._AppName;
            _TitleTextBox.Text = currentAppProcess._Title;
            // Console.WriteLine(currentAppProcess._Title);

            TimeSpan dt = TimeSpan.Zero;
            if (lastAppProcess != null)
            {
                dt = currentAppProcess.datetime - lastAppProcess.datetime;
            }

            if (chunk_ht.Contains(key))
            {
                ((App_Cunk)chunk_ht[key]).last_time = DateTime.Now;
                ((App_Cunk)chunk_ht[key]).total_time += dt;
            }
            else
            {
                var nc = new App_Cunk();
                nc.last_time = DateTime.Now;
                nc.total_time += dt;
                chunk_ht.Add(key, nc);
            }

            // Clone currentAppProcess to lastAppProcess.
            using (MemoryStream memoryStream = new MemoryStream())
            {
                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(memoryStream, currentAppProcess);
                memoryStream.Seek(0, SeekOrigin.Begin);
                lastAppProcess = (ActiveWindow)formatter.Deserialize(memoryStream);
            }
        }

        DateTime last_sent_input_time = DateTime.MinValue;
        string last_sent_info_input_key = null;
        DateTime last_sent_app_time = DateTime.MinValue;
        string last_sent_app_key = null;

        DateTime last_need_to_update_input_time = DateTime.MaxValue;
        DateTime last_need_to_update_app_time = DateTime.MaxValue;

        private void Info_Notify(bool force_send_last_app = false)
        {
            //write_line_to_debug("in Info_Notify");
            string current_app_title = get_most_used_app_title();
            ActiveWindow currentAppProcess = getCurrentAppDetails();
            string app_title2 = currentAppProcess._AppName + "|" + currentAppProcess._Title;
            if (force_send_last_app || current_app_title == null)
            {
                current_app_title = app_title2;
            }

            var send_info_input_key = LastKeyborad_Time + "|" + LastMouse_Time;

            bool input_changed = send_info_input_key != last_sent_info_input_key;
            bool app_changed = current_app_title != last_sent_app_key;

            DateTime now = DateTime.Now;

            bool need_to_update_input = input_changed && (now - last_sent_input_time).TotalMilliseconds > timeout_sec * 1000; // 10 sec
            bool need_to_update_app = app_changed && (now - last_sent_app_time).TotalMilliseconds > timeout_sec * 1000; // 10 sec

            //write_line_to_debug($"input_changed: {input_changed} app_changed: {app_changed} total_input_sec: {(DateTime.Now - last_sent_input_time).TotalSeconds} total_app_sec: {(DateTime.Now - last_sent_app_time).TotalSeconds} ");

            if (need_to_update_input && last_need_to_update_input_time.Year == DateTime.MaxValue.Year)
            {
                last_need_to_update_input_time = now;
                need_to_update_input = false;
                write_line_to_debug("first need to update input");
            }
            if (need_to_update_app && last_need_to_update_app_time.Year == DateTime.MaxValue.Year)
            {
                last_need_to_update_app_time = now;
                need_to_update_app = false;
                write_line_to_debug("first need to update app");
            }

            if (!need_to_update_input && !need_to_update_app)
            {
                //write_line_to_debug("not sending");
                return;
            }

            bool still_wait_to_send_input = (now - last_need_to_update_input_time).TotalMilliseconds <= last_sent_timeout * 1000; // 1 sec
            bool still_wait_to_send_app = (now - last_need_to_update_app_time).TotalMilliseconds <= last_sent_timeout * 1000; // 1 sec

            if (still_wait_to_send_input && still_wait_to_send_app)
            {
                return;
            }


            /******************/
            /* Before Sending */
            /******************/

            last_sent_info_input_key = send_info_input_key;
            last_sent_input_time = now;
            last_sent_app_key = current_app_title;
            last_sent_app_time = now;

            last_need_to_update_input_time = DateTime.MaxValue;
            last_need_to_update_app_time = DateTime.MaxValue;

            /********/
            /* SEND */
            /********/

            string[] app_string_splited = current_app_title.Split('|');

            var json = new
            {
                op = "client_info",
                connection_id = my_connection_id,
                from_whom = my_login_name,
                //to_whom_to_send = to_whom_to_send,
                App = app_string_splited[0],
                Title = app_string_splited[1],
                Last_keyboard_time = LastKeyborad_Time,
                Last_mouse_time = LastMouse_Time,
                Current_time = now,
            };

            if (Properties.Settings.Default.SendData)
            {
                string json_str = JsonConvert.SerializeObject(json);
                wss.Send(json_str);
            }

            chunk_ht = new Hashtable();

            write_line_to_debug("SENT info.");
        }

        private void write_line_to_debug(string text)
        {
            _uiSyncContext.Post(state =>
            {
                if (!PrintDebugCheckBox.Checked) return;
                DebugTextBox.Text += text + "\r\n";
                if (!ScrollCheckBox.Checked) return;
                DebugTextBox.SelectionStart = DebugTextBox.Text.Length;
                DebugTextBox.ScrollToCaret();
            }, null);
        }

        private string get_most_used_app_title()
        {
            var used_apps_titles_sorted_list = chunk_ht
                .Cast<DictionaryEntry>()
                .Where(entry =>
                {
                    if (entry.Value is App_Cunk notifyCunk)
                    {
                        return notifyCunk.total_time.TotalMilliseconds > 100;
                    }
                    return false;
                })
                .OrderByDescending(entry =>
                {
                    if (entry.Value is App_Cunk notifyCunk)
                    {
                        return notifyCunk.total_time;
                    }
                    return TimeSpan.Zero;
                })
                .Select(entry => entry.Key)
                .ToList();

            if (used_apps_titles_sorted_list.Count == 0)
            {
                return null; // lilo
            }

            string app_title = (string)used_apps_titles_sorted_list.First();
            return app_title;
        }

        void Display_Update_Process_Tick(object sender, EventArgs e) // Every 500 ms
        {
            if (lastRemoteUpdateTime != null)
            {
                string span_time_str;
                TimeSpan? span_time = DateTime.Now - lastRemoteUpdateTime + lastUpdateShiftTime;
                if (span_time?.TotalDays > 365) // lilo: no need for: span_time == null || ?
                    span_time_str = "Unknown";
                else
                    span_time_str = span_time?.ToString("hh\\:mm\\:ss"); // lilo: + lastRemoteUpdateTime?

                this.Invoke(new Action(() => {
                    _lastActionBeforeTextBox.Text = span_time_str;
                    if (notifyWhenActiveCheckBox.Checked && _traceeStatusTextBox.Text == "Online" && span_time < TimeSpan.FromSeconds(20)) // lilo
                    {
                        player.Open(new Uri(AppDomain.CurrentDomain.BaseDirectory + @"sounds/Active.mp3"));
                        //player.Position = TimeSpan.Zero;
                        player.Play();
                        notifyWhenActiveCheckBox.Checked = false;
                        notifyWhenOfflineCheckBox.Checked = true;
                        // ShowNotification(to_whom_to_send);
                    }
                    //if (_notifyWhenOnlineCheckBox.Enabled != (span_time > notify_timeout || _traceeStatusTextBox.Text != "Online"))
                    //{
                    //    // Debug
                    //}
                    notifyWhenActiveCheckBox.Enabled = span_time > notify_timeout || _traceeStatusTextBox.Text != "Online";
                    if (span_time > notify_timeout && notifyWhenInactiveCheckBox.Checked)
                    {
                        player.Open(new Uri(AppDomain.CurrentDomain.BaseDirectory + @"sounds/Offline.mp3"));
                        player.Play();
                        notifyWhenInactiveCheckBox.Checked = false;
                    }
                    notifyWhenInactiveCheckBox.Enabled = span_time < notify_timeout;
                }));
            }

            if (appUsedSince != null)
            {
                TimeSpan _since = DateTime.Now - (DateTime)appUsedSince;
                currentAppUsedSinceTextBox.Text = _since.ToString("hh\\:mm\\:ss");
                notifyWhenOverCheckBox.Enabled = _since > TimeSpan.FromSeconds(30);
            }

            updatePopupSinceTime();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
        }

        private void NotificationAreaIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                bool visible = !this.Visible;
                this.Visible = visible;
                if (visible)
                {
                    this.WindowState = FormWindowState.Normal;
                    this.TopMost = true; // lilo: trick
                    this.TopMost = Properties.Settings.Default.AlwaysOnTop;
                    //this.BringToFront();
                    //Thread.Sleep(500);
                    //this.Focus();
                }
                Console.WriteLine("this.WindowState: " + this.WindowState);
            }
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void HideOnStartCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.HideOnStart = HideOnStartCheckBox.Checked;
            Properties.Settings.Default.Save();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            this.Visible = !Properties.Settings.Default.HideOnStart;
            this.TopMost = Properties.Settings.Default.AlwaysOnTop;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        { // Make Form Invisible when minimized.
            if (WindowState == FormWindowState.Minimized)
            {
                this.Visible = false;
            }
        }

        private void AlwaysOnTopCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            bool _checked = AlwaysOnTopCheckBox.Checked;
            Properties.Settings.Default.AlwaysOnTop = _checked;
            Properties.Settings.Default.Save();
            this.TopMost = _checked;
        }

        private void SendDataCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.SendData = SendDataCheckBox.Checked;
            Properties.Settings.Default.Save();
        }

        private void SendAlsoAppsDataCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.SendAlsoAppsData = SendAlsoAppsDataCheckBox.Checked;
            Properties.Settings.Default.Save();
        }

        private void MyLoginNameTextBox_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.MyLoginName = MyLoginNameTextBox.Text;
            Properties.Settings.Default.Save();
        }

        private void ToWhomToSendTextBox_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.ToWhomToSend = ToWhomToSendTextBox.Text;
            Properties.Settings.Default.Save();
        }

        private void DisableStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Disable Notifier is not implemented yet."); // lilo3: todo
        }

        private void ClearDebugButton_Click(object sender, EventArgs e)
        {
            DebugTextBox.Text = "";
        }

        public void handle_dialog_response(string selected_response, string replyText)
        {
            var dialogResult = (selected_response ?? "") + " - " + replyText;

            this.Invoke(new Action(() => {
                dialogResultTextBox.Text = dialogResult;
                popupStatusTextBox.Text = "";
            }));


            send_custom_popup_response(dialogResult);
        }

        CustomPopupDialog customPopup;

        private void open_CustomPopupDialog_Click(object sender, EventArgs e)
        {
            open_CustomPopupDialog();
        }

        DateTime popupSentTime;
        DateTime popupRecievedTime;

        private void open_CustomPopupDialog()
        {
            List<string> choices_list = new List<string>();
            if (popup_choice_1_TextBox.Text != "")
            {
                choices_list.Add(popup_choice_1_TextBox.Text);
                if (popup_choice_2_TextBox.Text != "")
                {
                    choices_list.Add(popup_choice_2_TextBox.Text);
                    if (popup_choice_3_TextBox.Text != "")
                    {
                        choices_list.Add(popup_choice_3_TextBox.Text);
                    }
                    if (popup_choice_4_TextBox.Text != "")
                    {
                        choices_list.Add(popup_choice_4_TextBox.Text);
                    }
                }
            }

            if (popup_message_titleTextBox.Text == "")
            {
                MessageBox.Show("please enter popup_message_title");
                return;
            }

            var json = new
            {
                op = "show_custom_popup",
                to_whom_to_send = to_whom_to_send,
                popup_form_title = popup_form_titleTextBox.Text,
                popup_message_title = popup_message_titleTextBox.Text,
                choices_list = choices_list.ToArray(),
            };

            string json_str = JsonConvert.SerializeObject(json);
            wss.Send(json_str);

            popupSentTime = DateTime.Now;
            popupSentTimeTextBox.Text = popupSentTime.ToString("HH\\:mm\\:ss");
            popupRecievedTimeTextBox.Text = "";
            popupResponseTextBox.Text = "";
            closePopupButton.Enabled = true;
            sendPopupButton.Enabled = false;
        }

        private void runCustomPopup(string popup_form_title, string popup_message_title, List<string> choices_list)
        {
            this.Invoke(new Action(() =>
            {
                dialogResultTextBox.Text = "";
                popupStatusTextBox.Text = "";

                customPopup = new CustomPopupDialog(this);
                Thread thread = new Thread(() => customPopup.RunCustomPopupForm_Thread(popup_form_title, popup_message_title, choices_list));
                thread.Start();
            }));
        }

        private void closePopupButton_Click(object sender, EventArgs e)
        {
            var json = new
            {
                op = "close_custom_popup",
                to_whom_to_send = to_whom_to_send,
            };

            string json_str = JsonConvert.SerializeObject(json);
            wss.Send(json_str);
        }

        public void send_custom_popup_response(string popup_status)
        {
            var json = new
            {
                op = "custom_popup_response",
                to_whom_to_send = to_whom_to_send,
                dialog_result = popup_status,
            };

            //if (Properties.Settings.Default.SendData)
            //{
            string json_str = JsonConvert.SerializeObject(json);
            wss.Send(json_str);
            //}
        }

        private void clearPopupFormButton_Click(object sender, EventArgs e)
        {
            popupSentTimeTextBox.Text = "";
            popupRecievedTimeTextBox.Text = "";
            popupResponseTextBox.Text = "";
        }

        public void updatePopupSinceTime()
        {
            this.Invoke(new Action(() =>
            {
                if (popupSentTimeTextBox.Text != "")
                {
                    popupSentSinceTimeTextBox.Text = (DateTime.Now - popupSentTime).ToString("hh\\:mm\\:ss");
                }
                else
                {
                    popupSentSinceTimeTextBox.Text = "";
                }

                if (popupRecievedTimeTextBox.Text != "")
                {
                    popupRecievedSinceTimeTextBox.Text = (DateTime.Now - popupRecievedTime).ToString("hh\\:mm\\:ss");
                }
                else
                {
                    popupRecievedSinceTimeTextBox.Text = "";
                }
            }));
        }

        private static NotificationForm notificationForm;

        public enum NotificationStatus
        {
            Unknown,
            Online,
            Offline
        }

        public void ShowNotification(string tracee_name)
        {
            if (notificationForm == null || notificationForm.IsDisposed)
            {
                notificationForm = new NotificationForm();
            }

            NotificationStatus status;

            if (!Enum.TryParse(traceeStatusTextBox.Text, ignoreCase: false, out status))
            {
                throw new Exception("Unknown Status");
            }

            switch (status)
            {
                case NotificationStatus.Unknown:
                    notificationForm.BackColor = Color.LightGray;
                    notificationForm.ForeColor = Color.DimGray;
                    break;
                case NotificationStatus.Online:
                    notificationForm.BackColor = Color.LightGreen;
                    notificationForm.ForeColor = Color.DarkGreen;
                    break;
                case NotificationStatus.Offline:
                    notificationForm.BackColor = Color.LightCoral;
                    notificationForm.ForeColor = Color.DarkRed;
                    break;
                default:
                    throw new Exception();
            }

            int lastColonIndex = tracee_name.LastIndexOf(':');
            string tracee_name_without_pw = lastColonIndex >= 0 ? tracee_name.Substring(0, lastColonIndex) : tracee_name;
            notificationForm.SetMessage(tracee_name_without_pw + "\nStatus: " + traceeStatusTextBox.Text);

            Rectangle workingArea = Screen.GetWorkingArea(notificationForm);
            notificationForm.Location = new Point(
                workingArea.Right - notificationForm.Width - 10,
                workingArea.Bottom - notificationForm.Height - 10
            );

            if (!notificationForm.Visible)
            {
                notificationForm.Show();
            }
            else
            {
                notificationForm.BringToFront();
                notificationForm.Refresh();
            }
        }

        private void ShowNotificationButton_Click(object sender, EventArgs e)
        {
            ShowNotification(to_whom_to_send);
        }
    }
}