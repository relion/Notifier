using System;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Notifier
{
    public partial class MainForm : Form
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out IntPtr lpdwProcessId);

        public static string GetActiveWindowTitle()
        {
            IntPtr handle = GetForegroundWindow();
            const int nChars = 256;
            StringBuilder title = new StringBuilder(nChars);
            if (GetWindowText(handle, title, nChars) > 0)
            {
                return title.ToString();
            }
            return null;
        }

        [Serializable]
        class ActiveWindow
        {
            public DateTime datetime;
            public string ProcessName;
            public string AppName;
            public string Title;
            public string _AppName;
            public string _Title;
        }

        private ActiveWindow getCurrentAppDetails()
        {
            IntPtr activeAppHandle = GetForegroundWindow();
            IntPtr activeAppProcessId;
            GetWindowThreadProcessId(activeAppHandle, out activeAppProcessId);
            Process currentAppProcess = Process.GetProcessById((int)activeAppProcessId);

            string currentAppName = "";
            if (currentAppProcess.Id != 0)
            {
                try // lilo3: needed?
                {
                    var fileVersionInfo = FileVersionInfo.GetVersionInfo(currentAppProcess.MainModule.FileName);
                    currentAppName = fileVersionInfo.FileDescription;
                }
                catch (Exception ex) { }
            }

            var activeWindow = new ActiveWindow();
            activeWindow.datetime = DateTime.Now;

            try
            {
                if (currentAppProcess.ProcessName == "Idle")
                {
                    activeWindow.ProcessName = "Idle";
                }
                else
                {
                    activeWindow.ProcessName = currentAppProcess.ProcessName;
                }
            }
            catch(Exception)
            {
                activeWindow.ProcessName = "Idle";
            }

            bool found = false;
            try
            {
                activeWindow.AppName = currentAppName?? "";
                activeWindow.Title = GetActiveWindowTitle();
                //x.Icon = GetActiveWindowIconPath();
                found = true;
            }
            catch (Exception)
            {
            }

            if (!found || currentAppName == "Windows Explorer" && activeWindow.Title == "Program Manager")
            {
                activeWindow._AppName = "Idle";
                activeWindow._Title = "";
                return activeWindow;
            }

            int lastDashIndex = LastIndexOfRegEx(activeWindow.Title, "\\s[\\-]\\s"); // I notice another kind of dash but I could't reproduce it.
            if (lastDashIndex > 1)
            {
                activeWindow._Title = activeWindow.Title.Substring(0, lastDashIndex).Trim();
                var last_title_part = activeWindow.Title.Substring(lastDashIndex + 3).Trim();
                if (activeWindow.AppName == "")
                {
                    activeWindow._AppName = last_title_part;
                }
                else if (activeWindow.AppName.Contains(last_title_part))
                {
                    activeWindow._AppName = activeWindow.AppName;
                }
                else
                {
                    activeWindow._AppName = activeWindow.AppName;
                    activeWindow._Title = activeWindow.Title;
                }
            }
            else
            {
                if (activeWindow.AppName != "")
                {
                    activeWindow._AppName = activeWindow.AppName;
                }
                else
                {
                    // activeWindow._AppName = activeWindow.Title;
                    activeWindow._AppName = activeWindow.ProcessName;
                }

                if (activeWindow._AppName == activeWindow.Title)
                {
                    activeWindow._Title = "";
                }
                else
                {
                    activeWindow._Title = activeWindow.Title;
                }
            }

            return activeWindow;
        }
    }
}