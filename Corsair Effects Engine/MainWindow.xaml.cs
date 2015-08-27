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
        private bool WindowInitialized = false;
        private bool WindowClosing = false;
        private const double KEYBOARD_RATIO = 0.6;
        public KeyData[] keyData = new KeyData[144];
        private Button[] keyboardButtons = new Button[144];

        static Task EngineTask = null;

        #region MainWindow Events
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new CeeDataContext();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStatusMessage.NewMsg += UpdateStatusMessage_NewMsg;
            RefreshKeyboardPreview.ShowNewFrame += RefreshKeyboardPreview_ShowNewFrame;

            SetWindowLayout("LogSettings");

            // Initialize buttons for Keyboard Preview
            for (int i = 0; i < 144; i++)
            {
                keyboardButtons[i] = new Button();
                keyboardButtons[i].Visibility = System.Windows.Visibility.Hidden;
                KeyboardImage.Children.Add(keyboardButtons[i]);

                keyData[i] = new KeyData();
                keyData[i].KeyColor = new LightSolid(startColor: Color.FromRgb(255, 0, 0),
                                                       endColor: Color.FromRgb(0, 255, 0),
                                                       duration: 1);
            }

            GetDeviceIDs();
            StartEngine();

            WindowInitialized = true;
            UpdateStatusMessage.NewMessage(0, "Ready");
        }

        public void GetDeviceIDs()
        {
            switch (Properties.Settings.Default.KeyboardModel)
            {
                case "K65-RGB": DeviceHID.Keyboard = 0x1B17; break;
                case "K70-RGB": DeviceHID.Keyboard = 0x1B13; break;
                case "K95-RGB": DeviceHID.Keyboard = 0x1B11; break;
                case "STRAFE": DeviceHID.Keyboard = 0x1B15; break;
                default: DeviceHID.Keyboard = 0x0; break;
            }

            switch (Properties.Settings.Default.MouseModel)
            {
                case "M65 RGB": DeviceHID.Mouse = 0x1B12; break;
                case "Sabre Optical": DeviceHID.Mouse = 0x1B14; break;
                case "Sabre Laser": DeviceHID.Mouse = 0x1B19; break;
                case "Scimitar": DeviceHID.Mouse = 0x1B1E; break;
                default: DeviceHID.Mouse = 0x0; break;
            }
        }

        /// <summary>
        /// Prevent the window from closing until cleanup is done.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (WindowClosing == false) 
            {
                e.Cancel = true;
                WindowClosing = true;
                UpdateStatusMessage.NewMessage(0, "Shutting Down...");

                // Wait for engine to shut down
                StopEngine();
                await EngineTask;

                // Destroy thread-safe handles
                UpdateStatusMessage.NewMsg -= UpdateStatusMessage_NewMsg;
                RefreshKeyboardPreview.ShowNewFrame -= RefreshKeyboardPreview_ShowNewFrame;

                // Save settings
                Properties.Settings.Default.Save();

                // Close the window
                Application.Current.Windows[0].Close(); 
            }
        }

        private void StartEngine()
        {
            StopEngine();

            Engine.RunEngine = true;
            Engine.PauseEngine = false;

            EngineTask = Task.Run(() => Engine.Start());
        }

        private async void StopEngine()
        {
            if (EngineTask != null)
            {
                // Ask the thread to destroy itself
                Engine.RunEngine = false;

                // Wait for the thread to end
                await EngineTask;
            };
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
        {
            bool resumeEngineAfterLoad = false;
            if (Engine.RunEngine == true)
            {
                Engine.PauseEngine = true;
                resumeEngineAfterLoad = true;
                Thread.Sleep(200);
            }

            XmlToKeyMap xmlToKeyMap = new XmlToKeyMap();
            keyData = xmlToKeyMap.LoadKeyLocations(KeyboardModelComboBox.Text, "na");

            DrawButtonsOnKeyboard(keyData);

            SetWindowLayout("Keyboard");

            if (resumeEngineAfterLoad) { Engine.PauseEngine = false; };
        }

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
                            case "M65 RGB": mouseModelPath = "m65rgb\\image\\m65rgb.png"; break;
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
                        double keyboardRatio = KEYBOARD_RATIO;
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

        #region Thread-Safe Functions

        /// <summary>
        /// Posts a status message to the log and to console.
        /// </summary>
        /// <param name="messageType">Message level</param>
        /// <param name="messageText">Message text</param>
        public void UpdateStatusMessage_NewMsg(int messageType, string messageText)
        {
            if (LogTextBox == null) { return; };
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
            { 
                this.Dispatcher.Invoke(new Action(delegate { LogTextBox.AppendText(messagePrefix + messageText + "\r", logColour); } ));
                Console.WriteLine(messagePrefix + messageText);
            };
        }

        /// <summary>
        /// Updates the live keyboard preview.
        /// </summary>
        public void RefreshKeyboardPreview_ShowNewFrame()
        {
            UpdateStatusMessage.NewMessage(5, "New Frame");
            if (keyData == null) { return; };
            if (Engine.RunEngine == false || Engine.PauseEngine == true) { return; };
            UpdateStatusMessage.NewMessage(5, "Drawing Frame");
            for (int i = 0; i < keyData.Length; i++)
            {
                this.Dispatcher.Invoke(new Action(delegate { 
                            keyboardButtons[i].Background = new SolidColorBrush(
                                Color.FromArgb(127,
                                                keyData[i].KeyColor.LightColor.R,
                                                keyData[i].KeyColor.LightColor.G,
                                                keyData[i].KeyColor.LightColor.B)); } ));
            }
        }

        #endregion Thread-Safe Functions

        // Test Button
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            
            for (int k = 0; k < keyData.Length; k++) {
                /*
                keyData[k].KeyColor = new LightSolid(startColor: Color.FromRgb(255, 0, 0), 
                                                       endColor: Color.FromRgb(0, 255, 0),
                                                       duration: 5000);
                */
                keyData[k].KeyColor = new LightFade(startColor: Color.FromRgb(0, 255, 0),
                                                    endColor: Color.FromRgb(255, 0, 0),
                                                    solidDuration: 0,
                                                    totalDuration: 3000);
            }
        }

        #region Live Keyboard Preview

        private void DrawButtonsOnKeyboard(KeyData[] keyData)
        {
            int offsetX = 0;
            int offsetY = 0;

            double ButtonRatioX = KEYBOARD_RATIO;
            double ButtonRatioY = KEYBOARD_RATIO;

            if (Properties.Settings.Default.KeyboardModel == "STRAFE")
            {
                ButtonRatioX = .579;
                ButtonRatioY = .58;
            }

            for (int i = 0; i < keyData.Length; i++)
            {
                keyboardButtons[i].Height = (int)(keyData[i].Coords[2].Y * ButtonRatioY) - (int)(keyData[i].Coords[0].Y * ButtonRatioY);
                keyboardButtons[i].Width = (int)(keyData[i].Coords[1].X * ButtonRatioX) - (int)(keyData[i].Coords[0].X * ButtonRatioX);
                keyboardButtons[i].Content = "";
                keyboardButtons[i].Name = "keyboardButtons" + i;
                keyboardButtons[i].Style = (Style)FindResource(ToolBar.ButtonStyleKey);
                keyboardButtons[i].Background = new SolidColorBrush(Color.FromArgb(127, 0, 0, 0));
                keyboardButtons[i].Visibility = System.Windows.Visibility.Visible;

                Canvas.SetLeft(keyboardButtons[i], (int)(keyData[i].Coords[0].X * ButtonRatioX + offsetX));
                Canvas.SetTop(keyboardButtons[i], (int)(keyData[i].Coords[0].Y * ButtonRatioY + offsetY));
            }
            for (int u = keyData.Length; u < 144; u++)
            {
                keyboardButtons[u].Visibility = System.Windows.Visibility.Hidden;
            }
        }

        private void DrawButtonsOnKeyboard(bool Clear)
        {
            if (Clear)
            {
                for (int u = 0; u < 144; u++)
                {
                    keyboardButtons[u].Visibility = System.Windows.Visibility.Hidden;
                }
            }
        }

        private void KeyboardAndMouseSettingsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WindowInitialized == false) { return; };
            if (KeyboardModelComboBox == null || KeyboardLayoutComboBox == null) { return; };

            GetDeviceIDs();

            string Model = (KeyboardModelComboBox as ComboBox).SelectedItem.ToString();
            string Region = (KeyboardLayoutComboBox as ComboBox).SelectedItem.ToString();
            if (Model == "" || Region == "") { return; };
            if (Model == "None" || Region == "None") { DrawButtonsOnKeyboard(Clear: true); return; };

            Engine.RestartEngine = true;
        }

        #endregion Live Keyboard Preview
    }

    #region Data Classes

    public static class KeyboardMap
    {
        public static byte[] Positions;
        public static float[] Sizes;
        public static int CanvasWidth;
        public static byte[,] LedMatrix = new byte[7, 104];
    }

    public static class DeviceHID
    {
        public static uint Keyboard;
        public static uint Mouse;
    }

    #endregion Data Classes

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
                box.ScrollToEnd();
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

    public delegate void KeyboardPreviewDelegate();
    public static class RefreshKeyboardPreview
    {
        public static Window MainWindow;
        public static event KeyboardPreviewDelegate ShowNewFrame;

        public static void NewFrame()
        {
            ThreadSafeKeyboardPreview();
        }

        private static void ThreadSafeKeyboardPreview()
        {
            if (MainWindow != null && MainWindow.Dispatcher.CheckAccess())
            {
                MainWindow.Dispatcher.Invoke(new KeyboardPreviewDelegate(ThreadSafeKeyboardPreview));
            }
            else
            {
                ShowNewFrame();
            }
        }
    }

    #endregion Thread Delegates
}