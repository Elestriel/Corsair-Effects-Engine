using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using CSCore;
using CSCore.CoreAudioAPI;

namespace Corsair_Effects_Engine
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region MainWindow Events
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new CeeDataContext();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStatusMessage.NewMsg += UpdateStatusMessage_NewMsg;

            UpdateStatusMessage.NewMessage(4, "Searching for audio devices");
            // Refresh 

                // TODO: Start automatically when program starts

                UpdateStatusMessage.NewMessage(0, "Ready");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateStatusMessage.NewMessage(0, "Shutting Down...");
            // Wait for engine to shut down
            // TODO: Engine thread stop

            // Destroy thread-safe handle
            UpdateStatusMessage.NewMsg -= UpdateStatusMessage_NewMsg;

            // Save settings
            Properties.Settings.Default.Save();
        }

        #endregion MainWindow Events

        #region Layout Configurations

        private void LogButton_Click(object sender, RoutedEventArgs e)
        {
            if (GridRightLog.Visibility == System.Windows.Visibility.Hidden)
            { SetWindowLayout("LogSettings"); }
            else
            { SetWindowLayout("Log"); };
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        { SetWindowLayout("Settings"); }

        private void MouseButton_Click(object sender, RoutedEventArgs e)
        { SetWindowLayout("Mouse"); }

        private void KeyboardButton_Click(object sender, RoutedEventArgs e)
        { SetWindowLayout("Keyboard"); }

        private void SetWindowLayout(string mode)
        {
            HideAllGrids();
            switch (mode)
            {
                case "Log":
                    UpdateStatusMessage.NewMessage(7, "Log");
                    ContentLeft.Width = new GridLength(700, GridUnitType.Star);
                    ContentRight.Width = new GridLength(0);
                    GridContent.Visibility = System.Windows.Visibility.Visible;
                    GridLeftLog.Visibility = System.Windows.Visibility.Visible;
                    break;
                case "LogSettings":
                    UpdateStatusMessage.NewMessage(7, "LogSettings");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star);
                    GridContent.Visibility = System.Windows.Visibility.Visible;
                    GridLeftLog.Visibility = System.Windows.Visibility.Visible;
                    GridRightLog.Visibility = System.Windows.Visibility.Visible;
                    break;
                case "Settings":
                    UpdateStatusMessage.NewMessage(7, "Settings");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star);
                    GridContent.Visibility = System.Windows.Visibility.Visible;
                    GridLeftSettings.Visibility = System.Windows.Visibility.Visible;
                    GridRightSettings.Visibility = System.Windows.Visibility.Visible;
                    break;
                case "Mouse":
                    UpdateStatusMessage.NewMessage(7, "Mouse");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star); 
                    GridKeyboard.Visibility = System.Windows.Visibility.Visible;
                    KeyboardImage.Visibility = System.Windows.Visibility.Visible;

                    if (Properties.Settings.Default.MouseModel != "None" &&
                        Properties.Settings.Default.MouseModel != "")
                    {
                        string mouseModelPath = "";
                        switch (Properties.Settings.Default.MouseModel)
                        {
                            case "M65-RGB": mouseModelPath = "m65rgb\\image\\m65rgb.png"; break;
                            case "Saber Laser": mouseModelPath = "sabre1\\image\\sabre1-lighting.png"; break;
                            case "Saber Optical": mouseModelPath = "sabre2\\image\\sabre2-lighting.png"; break;
                            case "Scimitar": mouseModelPath = "scimitar\\image\\scimitar.png"; break;
                        }

                        BitmapImage mouseImage = new BitmapImage(new Uri(Environment.CurrentDirectory + "\\CorsairDevices\\" + mouseModelPath));

                        this.KeyboardImage.Width = mouseImage.Width * 0.6;
                        this.KeyboardImage.Height = mouseImage.Height * 0.6;
                        this.KeyboardImage.Background = new ImageBrush(mouseImage);
                    }

                    break;
                case "Keyboard":
                    UpdateStatusMessage.NewMessage(7, "Keyboard");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star);
                    GridKeyboard.Visibility = System.Windows.Visibility.Visible;
                    KeyboardImage.Visibility = System.Windows.Visibility.Visible;

                    if (Properties.Settings.Default.KeyboardModel != "None" && 
                        Properties.Settings.Default.KeyboardModel != "" && 
                        Properties.Settings.Default.KeyboardLayout != "None" &&
                        Properties.Settings.Default.KeyboardLayout != "")
                    {
                        string keyboardModelPath = "";
                        double keyboardRatio = 0.6;
                        switch (Properties.Settings.Default.KeyboardModel)
                        {
                            case "K65-RGB": keyboardModelPath = "cgk65rgb"; break;
                            case "K70-RGB": keyboardModelPath = "k70rgb"; break;
                            case "K95-RGB": keyboardModelPath = "k95rgb"; break;
                            case "STRAFE": keyboardModelPath = "strafe"; keyboardRatio = 1.2; break;
                            case "STRAFE-RGB": keyboardModelPath = "strafergb"; break;
                        }
                        string keyboardLayout = "";
                        switch (Properties.Settings.Default.KeyboardLayout)
                        {
                            case "Belgium": keyboardLayout = "be"; break;
                            case "Brazil": keyboardLayout = "br"; break;
                            case "China": keyboardLayout = "ch"; break;
                            case "European Union": keyboardLayout = "eu"; break;
                            case "France": keyboardLayout = "fr"; break;
                            case "Germany": keyboardLayout = "de"; break;
                            case "Italy": keyboardLayout = "it"; break;
                            case "Japan": keyboardLayout = "jp"; break;
                            case "Korea": keyboardLayout = "kr"; break;
                            case "Mexico": keyboardLayout = "mex"; break;
                            case "North America": keyboardLayout = "na"; break;
                            case "Nordic": keyboardLayout = "nd"; break;
                            case "Russia": keyboardLayout = "ru"; break;
                            case "Spain": keyboardLayout = "es"; break;
                            case "Taiwan": keyboardLayout = "tw"; break;
                            case "United Kingdom": keyboardLayout = "uk"; break;
                        }
                        
                        BitmapImage keyboardImage = new BitmapImage(new Uri(Environment.CurrentDirectory + "\\CorsairDevices\\" + keyboardModelPath + "\\image\\" + keyboardLayout + ".jpg"));

                        this.KeyboardImage.Width = keyboardImage.Width * keyboardRatio;
                        this.KeyboardImage.Height = keyboardImage.Height * keyboardRatio;
                        this.KeyboardImage.Background = new ImageBrush(keyboardImage);
                    }
                    break;
            }
        }

        private void HideAllGrids()
        {
            GridContent.Visibility = System.Windows.Visibility.Hidden;

            GridLeftLog.Visibility = System.Windows.Visibility.Hidden;
            GridLeftSettings.Visibility = System.Windows.Visibility.Hidden;

            GridRightColours.Visibility = System.Windows.Visibility.Hidden;
            GridRightLog.Visibility = System.Windows.Visibility.Hidden;
            GridRightSettings.Visibility = System.Windows.Visibility.Hidden;

            GridKeyboard.Visibility = System.Windows.Visibility.Hidden;
            KeyboardImage.Visibility = System.Windows.Visibility.Hidden;
        }

        #endregion Layout Configurations

        #region Settings Events

        private void CuePathTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.InitialDirectory = "C:\\Program Files (x86)\\Corsair\\Corsair Utility Engine";
            openFileDialog.Filter = "CUE (CorsairHID.exe)|CorsairHID.exe|Executable Files (*.exe)|*.exe|All Files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == true)
            { 
                Properties.Settings.Default.OptCuePath = openFileDialog.FileName;
            };
        }

        #endregion Settings Events

        /// <summary>
        /// Posts a status message to the log and to console.
        /// </summary>
        /// <param name="messageType">Message level</param>
        /// <param name="messageText">Message text</param>
        public void UpdateStatusMessage_NewMsg(int messageType, string messageText)
        {
            string messagePrefix;
            string logColour;

            // Determine the colour and prefix for the supplied messageType
            switch (messageType)
            {
                case 1:
                    messagePrefix = "Message: ";
                    logColour = "#50D060";
                    break;
                case 2:
                    messagePrefix = "Warning: ";
                    logColour = "#FFC040";
                    break;
                case 3:
                    messagePrefix = "Error  : ";
                    logColour = "#FF4040";
                    break;
                case 4:
                    messagePrefix = "Device : ";
                    logColour = "#B080C0";
                    break;
                case 5:
                    messagePrefix = "Thread : ";
                    logColour = "#FF50C0";
                    break;
                case 6:
                    messagePrefix = "Engine : ";
                    logColour = "#30C0B0";
                    break;
                case 7:
                    messagePrefix = "Layout : ";
                    logColour = "#E0D0A0";
                    break;
                default:
                    messagePrefix = "General: ";
                    logColour = "#D0D0D0";
                    break;
            }

            int LogLevel = Int32.Parse(Properties.Settings.Default.LogLevel.Substring(0, 1));
            if (messageType <= LogLevel)
            { this.Dispatcher.Invoke(new Action(delegate { LogTextBox.AppendText(messagePrefix + messageText + "\r", logColour); })); };
        }

    }

    #region Class Extensions

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
            }
            catch (FormatException) { }
        }
    }

    #endregion Class Extensions

    #region Thread Delegates

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

    #endregion Thread Delegates
}