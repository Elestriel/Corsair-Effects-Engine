using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Net;

namespace SelfUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Arguments CommandLineArgs;
        private string CLArgCurrentVersion = "No Parameter";
        private string CLArgNewVersion = "No Parameter";
        private string CLArgNewVersionURL = "No Parameter";
        private string CLArgAccentColour = "No Parameter";
        private bool CLArgsAreGood = true;
        private int DownloadProgress;
        WebClient webClient = new WebClient();

        public MainWindow()
        {
            InitializeComponent();

            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;

            // Declarations for custom window layout
            this.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, this.OnCloseWindow));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, this.OnMaximizeWindow, this.OnCanResizeWindow));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, this.OnMinimizeWindow, this.OnCanMinimizeWindow));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, this.OnRestoreWindow, this.OnCanResizeWindow));

            UpdateStatusMessage.NewMsg += UpdateStatusMessage_NewMsg;
            webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(Download_Completed);
            webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(Download_ProgressChanged);

            CommandLineArgs = new Arguments(Environment.GetCommandLineArgs());

            if (CommandLineArgs["CurrentVersion"] != null) { CLArgCurrentVersion = CommandLineArgs["CurrentVersion"]; }
            else { CLArgsAreGood = false; };
            if (CommandLineArgs["NewVersion"] != null) 
            { 
                CLArgNewVersion = CommandLineArgs["NewVersion"];
                CLArgNewVersionURL = "http://emily-maxwell.com/pages/cee/archive/CEE-Build-" + CLArgNewVersion + "-Update.zip";
            }
            else { CLArgsAreGood = false; };
            if (CommandLineArgs["AccentColour"] != null) { CLArgAccentColour = CommandLineArgs["AccentColour"];}
            else { CLArgsAreGood = false; };

            if (CLArgsAreGood) 
            { 
                Properties.Settings.Default.AccentColor = CLArgAccentColour;
                UpdateStatusMessage.NewMessage(0, "You are running Build " + CLArgCurrentVersion + ".");
                UpdateStatusMessage.NewMessage(0, "Select Update to download and install Build " + CLArgNewVersion + ".");
            }
            else 
            {
                UpdateStatusMessage.NewMessage(2, "Please do not launch the updater manually.");
                UpdateButton.IsEnabled = false;
            }
        }

        #region Methods for custom window

        private void OnCanResizeWindow(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.ResizeMode == ResizeMode.CanResize || this.ResizeMode == ResizeMode.CanResizeWithGrip;
        }

        private void OnCanMinimizeWindow(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.ResizeMode != ResizeMode.NoResize;
        }

        private void OnCloseWindow(object target, ExecutedRoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }

        private void OnMaximizeWindow(object target, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MaximizeWindow(this);
        }

        private void OnMinimizeWindow(object target, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void OnRestoreWindow(object target, ExecutedRoutedEventArgs e)
        {
            SystemCommands.RestoreWindow(this);
        }

        #endregion Methods for custom window

        /// <summary>
        /// Posts a status message to the log and to console.
        /// </summary>
        /// <param name="messageType">Message level</param>
        /// <param name="messageText">Message text</param>
        public void UpdateStatusMessage_NewMsg(int messageType, string messageText)
        {
            if (LogTextBox == null) { return; };
            string logColour;

            switch (messageType)
            {
                case 0: logColour = "#FF8080FF"; break; // Normal Messages
                case 1: logColour = "#FF80FF80"; break; // Successes
                case 2: logColour = "#FFFF8080"; break; // Errors
                case 3: logColour = "#FFA0FFA0"; break; // Download Progress
                default: logColour = "#FFFFFFFF"; break;
            }

            this.Dispatcher.Invoke(new Action(delegate { LogTextBox.AppendText(messageText + "\r", logColour); }));
            Console.WriteLine(messageText);
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadProgress = 0;
            RunUpdate_GetPackage();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows[0].Close(); 
        }

        private void RunUpdate_GetPackage()
        {
            UpdateStatusMessage.NewMessage(1, "Downloading Update.");
            
            webClient.DownloadFileAsync(new Uri(CLArgNewVersionURL), Environment.CurrentDirectory + "\\update.zip");
        }

        private void RunUpdate_ExtractPackage()
        {
            UpdateStatusMessage.NewMessage(1, "Extracting Update.");
            string zipPath = Environment.CurrentDirectory + "\\update.zip";
            string extPath = Environment.CurrentDirectory;
            
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry zae in archive.Entries) 
                {
                    UpdateStatusMessage.NewMessage(3, "Extracting: " + zae.FullName);
                    try { zae.ExtractToFile(Path.Combine(extPath, zae.FullName), true); }
                    catch (Exception e) { UpdateStatusMessage.NewMessage(2, e.ToString()); }
                }
            }
            
            UpdateStatusMessage.NewMessage(1, "Cleaning Up.");

            try { File.Delete(Environment.CurrentDirectory + "\\update.zip"); }
            catch { }

            UpdateStatusMessage.NewMessage(1, "Done!");

            UpdateButton.IsEnabled = false;
            CancelButton.Content = "Close";
        }

        private void Download_ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage >= 25 && DownloadProgress == 0) 
            {
                DownloadProgress = 1;
                UpdateStatusMessage.NewMessage(3, "25% Complete...");
            }
            if (e.ProgressPercentage >= 50 && DownloadProgress == 1) 
            {
                DownloadProgress = 2;
                UpdateStatusMessage.NewMessage(3, "50% Complete...");
            }
            if (e.ProgressPercentage >= 75 && DownloadProgress == 2) 
            {
                DownloadProgress = 3;
                UpdateStatusMessage.NewMessage(3, "75% Complete...");
            }
        }

        private void Download_Completed(object sender, AsyncCompletedEventArgs e) 
        {
            if (DownloadProgress == 0)
            { 
                UpdateStatusMessage.NewMessage(2, "Failed to download update.");
                try { File.Delete(Environment.CurrentDirectory + "\\update.zip"); }
                catch { }
            }
            else
            {
                UpdateStatusMessage.NewMessage(3, "100% Complete.");
                RunUpdate_ExtractPackage();
            }
        }
    }
    
    public static class ClassExtensions
    {
        public static void AppendText(this RichTextBox box, string text, string color)
        {
            BrushConverter bc = new BrushConverter();
            TextRange tr = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd);
            tr.Text = text;
            try
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty,
                    bc.ConvertFrom(color));
                box.ScrollToEnd();
            }
            catch (FormatException) { }
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        //Set to true if you want to show control when boolean value is true
        //Set to false if you want to hide/collapse control when value is true
        private bool triggerValue = false;
        public bool TriggerValue
        {
            get { return triggerValue; }
            set { triggerValue = value; }
        }
        //Set to true if you just want to hide the control
        //else set to false if you want to collapse the control
        private bool isHidden;
        public bool IsHidden
        {
            get { return isHidden; }
            set { isHidden = value; }
        }

        private object GetVisibility(object value)
        {
            if (!(value is bool))
                return DependencyProperty.UnsetValue;
            bool objValue = (bool)value;
            if ((objValue && TriggerValue && IsHidden) || (!objValue && !TriggerValue && IsHidden))
            {
                return Visibility.Hidden;
            }
            if ((objValue && TriggerValue && !IsHidden) || (!objValue && !TriggerValue && !IsHidden))
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return GetVisibility(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public delegate void AddStatusMessageDelegate(int messageType, string messageText);
    public static class UpdateStatusMessage
    {
        public static Window MainWindow;
        public static event AddStatusMessageDelegate NewMsg;

        public static void NewMessage(int messageType, string messageText)
        {
            ThreadSafeStatusMessage(messageType, messageText);
        }

        private static void ThreadSafeStatusMessage(int messageType, string messageText)
        {
            if (MainWindow != null && MainWindow.Dispatcher.CheckAccess())  // we are in a different thread to the main window
            {
                MainWindow.Dispatcher.Invoke(new AddStatusMessageDelegate(ThreadSafeStatusMessage), new object[] { messageType, messageText });  // call self from main thread
            }
            else
            {
                NewMsg(messageType, messageText);
            }
        }
    }

    public class Arguments
    {
        // Variables
        private StringDictionary Parameters;

        // Constructor
        public Arguments(string[] Args)
        {
            Parameters = new StringDictionary();
            Regex Spliter = new Regex(@"^-{1,2}|^/|=|:",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            Regex Remover = new Regex(@"^['""]?(.*?)['""]?$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            string Parameter = null;
            string[] Parts;

            // Valid parameters forms:
            // {-,/,--}param{ ,=,:}((",')value(",'))
            // Examples: 
            // -param1 value1 --param2 /param3:"Test-:-work" 
            //   /param4=happy -param5 '--=nice=--'
            foreach (string Txt in Args)
            {
                // Look for new parameters (-,/ or --) and a
                // possible enclosed value (=,:)
                Parts = Spliter.Split(Txt, 3);

                switch (Parts.Length)
                {
                    // Found a value (for the last parameter 
                    // found (space separator))
                    case 1:
                        if (Parameter != null)
                        {
                            if (!Parameters.ContainsKey(Parameter))
                            {
                                Parts[0] =
                                    Remover.Replace(Parts[0], "$1");

                                Parameters.Add(Parameter, Parts[0]);
                            }
                            Parameter = null;
                        }
                        // else Error: no parameter waiting for a value (skipped)
                        break;

                    // Found just a parameter
                    case 2:
                        // The last parameter is still waiting. 
                        // With no value, set it to true.
                        if (Parameter != null)
                        {
                            if (!Parameters.ContainsKey(Parameter))
                                Parameters.Add(Parameter, "true");
                        }
                        Parameter = Parts[1];
                        break;

                    // Parameter with enclosed value
                    case 3:
                        // The last parameter is still waiting. 
                        // With no value, set it to true.
                        if (Parameter != null)
                        {
                            if (!Parameters.ContainsKey(Parameter))
                                Parameters.Add(Parameter, "true");
                        }

                        Parameter = Parts[1];

                        // Remove possible enclosing characters (",')
                        if (!Parameters.ContainsKey(Parameter))
                        {
                            Parts[2] = Remover.Replace(Parts[2], "$1");
                            Parameters.Add(Parameter, Parts[2]);
                        }

                        Parameter = null;
                        break;
                }
            }
            // In case a parameter is still waiting
            if (Parameter != null)
            {
                if (!Parameters.ContainsKey(Parameter))
                    Parameters.Add(Parameter, "true");
            }
        }

        // Retrieve a parameter value if it exists 
        // (overriding C# indexer property)
        public string this[string Param]
        {
            get
            {
                return (Parameters[Param]);
            }
        }
    }
}