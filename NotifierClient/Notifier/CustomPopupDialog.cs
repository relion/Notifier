using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Notifier
{
    partial class CustomPopupDialog : Form
    {
        // Controls:
        RadioButton[] radioButtons;
        GroupBox groupBox;
        Button sendResultButton;

        // Popup info:
        public string popup_status = null;
        DateTime popup_timer_start_time;
        DateTime? last_popup_interaction_time = null;

        int popup_timeout_sec = 10;
        int no_action_timeout_sec = 20;

        // dialog result:
        int selected_index;
        string reply_text;

        MainForm main_form;

        public CustomPopupDialog(MainForm main_form)
        {
            InitializeComponent();
            this.main_form = main_form;
            Load += (sender, e) => { Activate(); TopMost = true; TopLevel = true; BringToFront(); };
        }

        private bool IsHebrewString(string text)
        {
            return Regex.IsMatch(text, @"^[^א-תa-zA-Z]*[א-ת]");
        }

        private int GetSelectedIndex(RadioButton[] rb)
        {
            if (rb == null) return -1;
            for (int i = 0; i < rb.Length; i++)
            {
                if (rb[i].Checked)
                {
                    return i;
                }
            }
            return -1; // None selected.
        }

        private void FormInputEvent(object sender, EventArgs e)
        {
            last_popup_interaction_time = DateTime.Now;
        }

        public void CreateCustomPopupForm(string form_title, string message_title, List<string> choices_list)
        {
            Text = form_title;
            StartPosition = FormStartPosition.CenterScreen;
            MouseMove += FormInputEvent;

            bool title_isHebrew = false;
            bool radio_isHebrew = false;

            TextBox titleTextBox = new TextBox();
            titleTextBox.Location = new Point(10, 10);
            titleTextBox.Multiline = true;
            // titleTextBox.WordWrap = true; // lilo3: what is the meaning of that?
            titleTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            titleTextBox.ReadOnly = true;
            titleTextBox.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold, GraphicsUnit.Point, ((byte)(177)));
            titleTextBox.BorderStyle = BorderStyle.None;
            titleTextBox.Text = message_title;
            if (IsHebrewString(message_title)) title_isHebrew = true;
            int titleWidth = TextRenderer.MeasureText(titleTextBox.Text, titleTextBox.Font).Width;
            titleTextBox.Width = titleWidth + 10;
            int titleHeight = TextRenderer.MeasureText(titleTextBox.Text, titleTextBox.Font).Height;
            titleTextBox.Height = titleHeight + 10;
            Controls.Add(titleTextBox);

            int max_groupBox_width = 0;
            int add_x = 0; // lilo: not needed.

            if (choices_list?.Any() == true)
            {
                groupBox = new GroupBox();
                groupBox.Text = "Select";
                groupBox.Location = new Point(10, titleTextBox.Height + 10);
                groupBox.MouseMove += FormInputEvent;
                groupBox.Width = 300;
                Controls.Add(groupBox);

                radioButtons = new RadioButton[choices_list.Count];
                int i;
                for (i = 0; i < choices_list.Count; i++)
                {
                    radioButtons[i] = new RadioButton();
                    radioButtons[i].Click += (object sender, EventArgs e) => {
                        sendResultButton.PerformClick();
                    };
                    var text = choices_list[i];
                    radioButtons[i].Text = text;

                    if (IsHebrewString(text)) radio_isHebrew = true;

                    // Adjust the Y position to place them vertically
                    radioButtons[i].Location = new Point(10, titleTextBox.Height + i * (radioButtons[i].Height + 5) - 10);
                    int textWidth = TextRenderer.MeasureText(radioButtons[i].Text, radioButtons[i].Font).Width + 20;
                    radioButtons[i].Width = 999; // lilo: textWidth;
                    if (textWidth > max_groupBox_width)
                    {
                        max_groupBox_width = textWidth;
                    }

                    groupBox.Controls.Add(radioButtons[i]);
                }
                int height_x9 = radioButtons[i - 1].Bottom;
                groupBox.Size = new Size(max_groupBox_width + 32 + add_x, height_x9 + 10);
            }

            Label replyLabel = new Label();
            replyLabel.Text = "Answer:";
            replyLabel.Width = TextRenderer.MeasureText(replyLabel.Text, replyLabel.Font).Width;
            replyLabel.Location = new Point(10, (groupBox?.Bottom ?? titleTextBox.Bottom) + 10);
            Controls.Add(replyLabel);

            TextBox replyTextBox = new TextBox();
            replyTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            replyTextBox.Width = Width - replyLabel.Width - 45;
            replyTextBox.Location = new Point(replyLabel.Width + 15, replyLabel.Location.Y);
            replyTextBox.Multiline = true;
            var line_height = TextRenderer.MeasureText("A", replyTextBox.Font).Height;
            var n_replyTextBox_lines = 3;
            replyTextBox.Size = new Size(replyTextBox.Size.Width, replyTextBox.Size.Height + (n_replyTextBox_lines - 1) * line_height);
            replyTextBox.TextChanged += FormInputEvent;

            Controls.Add(replyTextBox);

            Button send_result_button = new Button();
            sendResultButton = send_result_button;
            send_result_button.Text = "Send";
            send_result_button.Location = new Point(10, replyTextBox.Bottom + 10);
            send_result_button.Width = TextRenderer.MeasureText(send_result_button.Text, send_result_button.Font).Width + 10;
            send_result_button.Click += (sender2, e2) =>
            {
                selected_index = GetSelectedIndex(radioButtons);
                if (replyTextBox.Text.Trim() == "" && selected_index < 0)
                {
                    MessageBox.Show("נא למלא את השאלון או תשובה");
                    return;
                }

                reply_text = replyTextBox.Text;

                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(send_result_button);

            /* Handle RTL */

            if (title_isHebrew)
            {
                titleTextBox.Location = new Point(Width - titleTextBox.Width - 35, titleTextBox.Location.Y);
                titleTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                titleTextBox.RightToLeft = RightToLeft.Yes;
            }

            if (radio_isHebrew)
            {
                if (groupBox != null)
                {
                    groupBox.Text = "בחר/י";
                    groupBox.RightToLeft = RightToLeft.Yes;
                    groupBox.Location = new Point(Width - max_groupBox_width - 70 - add_x, groupBox.Location.Y);
                    groupBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                    for (int i2 = 0; i2 < choices_list.Count; i2++)
                    {
                        radioButtons[i2].Location = new Point(groupBox.Width - radioButtons[i2].Width - 15, radioButtons[i2].Location.Y);
                    }
                }
            }

            if (title_isHebrew || radio_isHebrew)
            {
                replyLabel.Text = "תשובה:";
                replyLabel.RightToLeft = RightToLeft.Yes;
                var x9_width = TextRenderer.MeasureText(replyLabel.Text, replyLabel.Font).Width;
                replyLabel.Width = x9_width;
                replyLabel.Location = new Point(Width - x9_width - 35, replyLabel.Location.Y);
                replyLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

                replyTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
                replyTextBox.RightToLeft = RightToLeft.Yes;
                replyTextBox.Width = Width - replyLabel.Width - 60;
                replyTextBox.Location = new Point(20, replyTextBox.Location.Y);

                send_result_button.Text = "שלח/י";
                send_result_button.Width = TextRenderer.MeasureText(send_result_button.Text, send_result_button.Font).Width + 10;
                send_result_button.Location = new Point(Width - send_result_button.Width - 35, send_result_button.Location.Y);
                send_result_button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            }

            /* Calculate and set the form size to fit its content */

            const int popup_min_width = 600;
            const int popup_max_width = 1200;

            var max_width = Math.Max(
                Math.Max(
                    titleWidth + 15,
                    groupBox?.Right ?? 0),
                replyTextBox.Right) + 30;
            Size = new Size(
                Math.Max(max_width, popup_min_width),
                send_result_button.Bottom + replyTextBox.Size.Height + 5
            );

            /* Disable resizing vertically */

            MinimumSize = new Size(Size.Width, Size.Height);
            MaximumSize = new Size(popup_max_width, Size.Height);

            /* Focus on replyTextBox when Shown */

            Shown += (sender, e) => replyTextBox.Focus();
        }

        public void RunCustomPopupForm_Thread(string form_title, string message_title, List<string> choices_list)
        {
            CreateCustomPopupForm(form_title, message_title, choices_list);

            last_popup_interaction_time = null;

            if (false)
            {
                DispatcherTimer Notify_Timer;
                Notify_Timer = new DispatcherTimer();
                Notify_Timer.Interval = new TimeSpan(0, 0, 0, 0, 500); // 200 mili seconds.
                Notify_Timer.Tick += PopUp_Tick;
                Notify_Timer.Start();
            }

            popup_timer_start_time = DateTime.Now;
            popup_status = "running";

            DialogResult result = ShowDialog();

            if (result == DialogResult.OK)
            {
                string selected_response = selected_index >= 0 ? choices_list?[selected_index] : null;
                string replyText = reply_text;

                main_form.handle_dialog_response(selected_response, replyText);
            }
            else if (result == DialogResult.Cancel)
            {
                try
                {
                    main_form.Invoke(new Action(() => {
                        switch (popup_status)
                        {
                            case "running":
                                main_form.popupStatusTextBox.Text = "Canceled by User";
                                break;

                            case "closed by operator":
                                main_form.popupStatusTextBox.Text = "Closed by Operator";
                                break;

                            case "closed due no action timeout":
                                main_form.popupStatusTextBox.Text = "Closed due No Action";
                                break;

                            case "closed due no interaction timeout":
                                main_form.popupStatusTextBox.Text = "Closed due No Interaction";
                                break;

                            default:
                                throw new Exception("Unrecognized popup_status: " + popup_status);
                        }

                        main_form.dialogResultTextBox.Text = "";

                        main_form.send_custom_popup_response(main_form.popupStatusTextBox.Text);
                    }));
                }
                catch (Exception e)
                {
                    // lilo4: usually because the MainForm was Closed.
                }
            }
            else
            {
                throw new Exception("Unhandled DialogResult: " + result);
            }

            Dispose();
        }

        private void PopUp_Tick(object sender, EventArgs e)
        {
            handle_popup_auto_close();
        }

        private void handle_popup_auto_close()
        {
            if (popup_status == "running") // customPopup != null)
            {
                bool do_close_popup = false;
                this.Invoke(new Action(() =>
                {
                    if (last_popup_interaction_time != null)
                    {
                        double since_last_popup_interaction_sec = (DateTime.Now - (DateTime)last_popup_interaction_time).TotalSeconds;
                        if (since_last_popup_interaction_sec > no_action_timeout_sec)
                        {
                            do_close_popup = true;
                            popup_status = "closed due no interaction timeout";
                        }
                        else
                        {
                            main_form.popupStatusTextBox.Text = "autoclosing (last interaction) in: " + (TimeSpan.FromSeconds(no_action_timeout_sec) - (DateTime.Now - (DateTime)last_popup_interaction_time)).TotalSeconds.ToString("F1") + " sec.";
                            //lastInteractionTextBox.Text = since_last_popup_interaction_sec.ToString("F1") + " sec.";
                        }
                    }
                    else if ((DateTime.Now - popup_timer_start_time).TotalSeconds > popup_timeout_sec)
                    {
                        do_close_popup = true;
                        popup_status = "closed due no action timeout";
                        main_form.popupStatusTextBox.Text = "";
                    }
                    else
                    {
                        main_form.popupStatusTextBox.Text = "autoclosing (no action) in: " + (TimeSpan.FromSeconds(popup_timeout_sec) - (DateTime.Now - popup_timer_start_time)).TotalSeconds.ToString("F1") + " sec.";
                    }
                }));

                if (do_close_popup)
                {
                    closeCustomPopup();
                }
            }
        }

        public void closeCustomPopup()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Dispose())); // lilo4: Close?
            }
            else
            {
                Dispose();
            }
        }
    }
}
