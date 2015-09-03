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
using ColorPicker;
using ColorPickerControls;
using ColorPickerControls.Dialogs;

namespace Corsair_Effects_Engine
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string VersionNumber = "0.1.0.0012";
        private bool WindowInitialized = false;
        private bool WindowClosing = false;
        private const double KEYBOARD_RATIO = 0.6;
        public static KeyData[] keyData = new KeyData[149];
        private Button[] keyboardButtons = new Button[144];
        private Button[] mouseButtons = new Button[5];

        // When editing colour settings, what to return to on accept/cancel
        private static string EditPageReturnTo = "";
        private static string InitialLowerColor;
        private static string InitialUpperColor;

        // Name of the page being edited
        private static string PageBeingEdited;

        Engine newEngine = new Engine();
        Task EngineTask = null;

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

            //SetWindowLayout("LogSettings");
            SetWindowLayout("Settings");

            // Initialize buttons for Keyboard Preview
            for (int i = 0; i < 144; i++)
            {
                keyboardButtons[i] = new Button();
                keyboardButtons[i].Visibility = System.Windows.Visibility.Hidden;
                KeyboardImage.Children.Add(keyboardButtons[i]);

                keyData[i] = new KeyData();
                keyData[i].KeyColor = new LightSwitch(startColor: Color.FromRgb(255, 255, 255),
                                                       endColor: Color.FromRgb(0, 0, 0),
                                                       duration: 0);
            }

            for (int i = 0; i < 5; i++)
            {
                mouseButtons[i] = new Button();
                mouseButtons[i].Visibility = System.Windows.Visibility.Hidden;

                keyData[i + 144] = new KeyData();
                keyData[i + 144].KeyColor = new LightSwitch(startColor: Color.FromRgb(255, 255, 255),
                                                       endColor: Color.FromRgb(0, 0, 0),
                                                       duration: 0);
                //KeyboardImage.Children.Add(keyboardButtons[i]);
            }

            GetDeviceIDs();
            StartEngine();

            WindowInitialized = true;
            UpdateStatusMessage.NewMessage(0, "Welcome to the Corsair Effects Engine v" + VersionNumber + ".");
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
                case "Saber Optical": DeviceHID.Mouse = 0x1B14; break;
                case "Saber Laser": DeviceHID.Mouse = 0x1B19; break;
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
            newEngine.PauseEngine = false;
            newEngine.RunEngine = true;
            EngineTask = Task.Run(() => newEngine.Start());
        }

        private async void StopEngine()
        {
            if (EngineTask != null)
            {
                // Ask the thread to destroy itself
                newEngine.RunEngine = false;

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
            /*
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
            */
        }

        private void ForegroundEditButton_Click(object sender, RoutedEventArgs e)
        {
            SetWindowLayout("ForegroundEdit", Properties.Settings.Default.ForegroundEffect);
        }

        private void BackgroundEditButton_Click(object sender, RoutedEventArgs e)
        {
            SetWindowLayout("BackgroundEdit", Properties.Settings.Default.BackgroundEffect);
        }

        private void StaticEditButton_Click(object sender, RoutedEventArgs e)
        {
            SetWindowLayout("StaticEdit");
        }

        private void SetWindowLayout(string mode, string mode2 = "")
        {
            HideAllGrids();
            switch (mode)
            {
                #region Log
                case "Log":
                    UpdateStatusMessage.NewMessage(7, "Log");
                    ContentLeft.Width = new GridLength(700, GridUnitType.Star);
                    ContentRight.Width = new GridLength(0);
                    GridContent.Visibility = System.Windows.Visibility.Visible;
                    GridLeftLog.Visibility = System.Windows.Visibility.Visible;
                    GridControls.IsEnabled = true;
                    break;
                case "LogSettings":
                    UpdateStatusMessage.NewMessage(7, "LogSettings");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star);
                    GridContent.Visibility = System.Windows.Visibility.Visible;
                    GridLeftLog.Visibility = System.Windows.Visibility.Visible;
                    GridRightLog.Visibility = System.Windows.Visibility.Visible;
                    GridControls.IsEnabled = true;
                    break;
                #endregion Log
                #region Settings
                case "Settings":
                    UpdateStatusMessage.NewMessage(7, "Settings");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star);
                    GridContent.Visibility = System.Windows.Visibility.Visible;
                    GridLeftSettings.Visibility = System.Windows.Visibility.Visible;
                    GridRightSettings.Visibility = System.Windows.Visibility.Visible;
                    GridControls.IsEnabled = true;
                    break;
                #endregion Settings
                #region Mouse
                case "Mouse":
                    UpdateStatusMessage.NewMessage(7, "Mouse");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star); 
                    GridKeyboard.Visibility = System.Windows.Visibility.Visible;
                    KeyboardImage.Visibility = System.Windows.Visibility.Visible;
                    GridControls.IsEnabled = true;

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
                #endregion Mouse
                #region Keyboard
                case "Keyboard":
                    UpdateStatusMessage.NewMessage(7, "Keyboard");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star);
                    GridKeyboard.Visibility = System.Windows.Visibility.Visible;
                    KeyboardImage.Visibility = System.Windows.Visibility.Visible;
                    GridControls.IsEnabled = true;

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
                #endregion Keyboard
                #region Foreground
                case "ForegroundEdit":
                    UpdateStatusMessage.NewMessage(7, "Foreground Edit");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star);
                    GridContent.Visibility = System.Windows.Visibility.Visible;
                    GridLeftEdit.Visibility = System.Windows.Visibility.Visible;
                    GridRightSettings.Visibility = System.Windows.Visibility.Visible;
                    GridControls.IsEnabled = false;

                    PageBeingEdited = mode2;
                    PageBeingEditedLabel.Content = "Currently Editing: " + PageBeingEdited;

                    switch (mode2)
                    {
                        case "Spectrograph":
                            break;
                        case "Random Lights":
                            GridForegroundRandomLights.Visibility = System.Windows.Visibility.Visible;
                            // Ensure the right controls are appearing based on selections
                            ForegroundRandomLightsStyle_SelectionChanged(null, null);
                            ForegroundRandomLightsStartType_SelectionChanged(null, null);
                            ForegroundRandomLightsEndType_SelectionChanged(null, null);
                            break;
                        case "Reactive Typing":
                            GridForegroundReactive.Visibility = System.Windows.Visibility.Visible;
                            // Ensure the right controls are appearing based on selections
                            ForegroundReactiveStyle_SelectionChanged(null, null);
                            ForegroundReactiveStartType_SelectionChanged(null, null);
                            ForegroundReactiveEndType_SelectionChanged(null, null);
                            break;
                        case "Heatmap":
                            GridForegroundHeatmap.Visibility = System.Windows.Visibility.Visible;
                            break;
                    }
                    break;
                #endregion Foreground
                #region Background
                case "BackgroundEdit":
                    UpdateStatusMessage.NewMessage(7, "Background Edit");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star);
                    GridContent.Visibility = System.Windows.Visibility.Visible;
                    GridLeftEdit.Visibility = System.Windows.Visibility.Visible;
                    GridBackground.Visibility = System.Windows.Visibility.Visible;
                    GridRightSettings.Visibility = System.Windows.Visibility.Visible;
                    GridControls.IsEnabled = false;

                    PageBeingEdited = mode2;
                    PageBeingEditedLabel.Content = "Currently Editing: " + PageBeingEdited;

                    switch (mode2)
                    {
                        case "Rainbow":
                            GridBackgroundRainbow.Visibility = System.Windows.Visibility.Visible;
                            break;
                    }
                    break;
                #endregion Foreground
                #region Static
                case "StaticEdit":

                    break;
                #endregion Foreground
            }
        }

        private void HideAllGrids()
        {
            // Content Panel
            GridContent.Visibility = System.Windows.Visibility.Hidden;

            // Content: Log
            GridLeftLog.Visibility = System.Windows.Visibility.Hidden;
            GridRightLog.Visibility = System.Windows.Visibility.Hidden;

            // Content: Settings
            GridLeftSettings.Visibility = System.Windows.Visibility.Hidden;
            GridRightSettings.Visibility = System.Windows.Visibility.Hidden;

            // Edit container
            GridLeftEdit.Visibility = System.Windows.Visibility.Hidden;

            // Edit: Colour
            GridColor.Visibility = System.Windows.Visibility.Hidden;
            
            // Edit: Foreground
            GridForegroundRandomLights.Visibility = System.Windows.Visibility.Hidden;
            GridForegroundReactive.Visibility = System.Windows.Visibility.Hidden;
            GridForegroundHeatmap.Visibility = System.Windows.Visibility.Hidden;

            // Edit: Background
            GridBackground.Visibility = System.Windows.Visibility.Hidden;
            GridBackgroundRainbow.Visibility = System.Windows.Visibility.Hidden;

            // Full Keyboard
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
        {/*
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
          */
        }

        #endregion Thread-Safe Functions
        
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

            //Engine.RestartEngine = true;
            newEngine.RestartEngine = true;
        }

        #endregion Live Keyboard Preview

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
        }

        #region Pages

        #region Page: ForegroundEdit

        #region Page: ForegroundEdit: Random Lights

        private void ForegroundRandomLightsStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!WindowInitialized) { return; };
            switch (Properties.Settings.Default.ForegroundRandomLightsStyle)
            {
                case "Switch":
                    ForegroundRandomLightsFadeSolidDurationUD.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundRandomLightsFadeTotalDurationUD.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundRandomLightsFadeSolidDurationLabel.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundRandomLightsFadeTotalDurationLabel.Visibility = System.Windows.Visibility.Visible;
                    ForegroundRandomLightsSolidDurationUD.Visibility = System.Windows.Visibility.Visible;
                    break;
                case "Fade": 
                    ForegroundRandomLightsFadeSolidDurationUD.Visibility = System.Windows.Visibility.Visible;
                    ForegroundRandomLightsFadeTotalDurationUD.Visibility = System.Windows.Visibility.Visible;
                    ForegroundRandomLightsFadeSolidDurationLabel.Visibility = System.Windows.Visibility.Visible;
                    ForegroundRandomLightsFadeTotalDurationLabel.Visibility = System.Windows.Visibility.Visible;
                    ForegroundRandomLightsSolidDurationUD.Visibility = System.Windows.Visibility.Hidden;
                    break;
            }
        }

        private void ForegroundRandomLightsStartType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!WindowInitialized) { return; };
            if (Properties.Settings.Default.ForegroundRandomLightsStartType == "Defined Colour")
            { ForegroundRandomLightsStartColor.Visibility = System.Windows.Visibility.Visible; }
            else { ForegroundRandomLightsStartColor.Visibility = System.Windows.Visibility.Hidden; }
        }

        private void ForegroundRandomLightsEndType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!WindowInitialized) { return; };
            if (Properties.Settings.Default.ForegroundRandomLightsEndType == "Defined Colour")
            { ForegroundRandomLightsEndColor.Visibility = System.Windows.Visibility.Visible; }
            else { ForegroundRandomLightsEndColor.Visibility = System.Windows.Visibility.Hidden; }
        }

        private void ForegroundRandomLightsStartColorEdit_Click(object sender, RoutedEventArgs e)
        {
            switch (Properties.Settings.Default.ForegroundRandomLightsStartType)
            {
                case "Defined Colour":
                    Properties.Settings.Default.ForegroundRandomLightsSwitchColorStart = OpenColorPicker(Properties.Settings.Default.ForegroundRandomLightsSwitchColorStart).ToString();
                    break;
                case "Random Colour":
                    EditPageReturnTo = "ForegroundRandomLightsStart";
                    ColourLabel.Content = "Start Colour";
                    InitialLowerColor = Properties.Settings.Default.ForegroundRandomLightsColorStartLower;
                    InitialUpperColor = Properties.Settings.Default.ForegroundRandomLightsColorStartUpper;

                    // Ugly: Assign property values to initialize sliders.
                    // This needs to be replaced with proper binding, somehow.
                    ColorSliders.LowerColor = (Color)ColorConverter.ConvertFromString(InitialLowerColor);
                    ColorSliders.UpperColor = (Color)ColorConverter.ConvertFromString(InitialUpperColor);

                    SetNewColorSlidersBinding("ForegroundRandomLightsColorStartLower",
                                              "ForegroundRandomLightsColorStartUpper");
            
                    GridColor.Visibility = System.Windows.Visibility.Visible;
                    DisableControlsWhileEditing();
                    GridForegroundRandomLights.IsEnabled = false;
                    break;
            }
        }

        private void ForegroundRandomLightsEndColorEdit_Click(object sender, RoutedEventArgs e)
        {
            switch (Properties.Settings.Default.ForegroundRandomLightsEndType)
            {
                case "Defined Colour":
                    Properties.Settings.Default.ForegroundRandomLightsSwitchColorEnd = OpenColorPicker(Properties.Settings.Default.ForegroundRandomLightsSwitchColorEnd).ToString();
                    break;
                case "Original Colour":
                    // Do nothing
                    break;
                case "Random Colour":
                    EditPageReturnTo = "ForegroundRandomHeadsEnd";
                    ColourLabel.Content = "End Colour";
                    InitialLowerColor = Properties.Settings.Default.ForegroundRandomLightsColorEndLower;
                    InitialUpperColor = Properties.Settings.Default.ForegroundRandomLightsColorEndUpper;

                    // Ugly: Assign property values to initialize sliders.
                    // This needs to be replaced with proper binding, somehow.
                    ColorSliders.LowerColor = (Color)ColorConverter.ConvertFromString(InitialLowerColor);
                    ColorSliders.UpperColor = (Color)ColorConverter.ConvertFromString(InitialUpperColor);

                    SetNewColorSlidersBinding("ForegroundRandomLightsColorEndLower",
                                              "ForegroundRandomLightsColorEndUpper");

                    GridColor.Visibility = System.Windows.Visibility.Visible;
                    DisableControlsWhileEditing();
                    GridForegroundRandomLights.IsEnabled = false;
                    break;
            }
        }

        #endregion Page: ForegroundEdit: Random Lights

        #region Page: ForegroundEdit: Reactive

        private void ForegroundReactiveStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!WindowInitialized) { return; };
            switch (Properties.Settings.Default.ForegroundReactiveStyle)
            {
                case "Switch":
                    ForegroundReactiveFadeSolidDurationUD.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundReactiveFadeTotalDurationUD.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundReactiveFadeSolidDurationLabel.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundReactiveFadeTotalDurationLabel.Visibility = System.Windows.Visibility.Visible;
                    ForegroundReactiveSolidDurationUD.Visibility = System.Windows.Visibility.Visible;
                    break;
                case "Fade":
                    ForegroundReactiveFadeSolidDurationUD.Visibility = System.Windows.Visibility.Visible;
                    ForegroundReactiveFadeTotalDurationUD.Visibility = System.Windows.Visibility.Visible;
                    ForegroundReactiveFadeSolidDurationLabel.Visibility = System.Windows.Visibility.Visible;
                    ForegroundReactiveFadeTotalDurationLabel.Visibility = System.Windows.Visibility.Visible;
                    ForegroundReactiveSolidDurationUD.Visibility = System.Windows.Visibility.Hidden;
                    break;
            }
        }

        private void ForegroundReactiveStartType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!WindowInitialized) { return; };
            if (Properties.Settings.Default.ForegroundReactiveStartType == "Defined Colour")
            { ForegroundReactiveStartColor.Visibility = System.Windows.Visibility.Visible; }
            else { ForegroundReactiveStartColor.Visibility = System.Windows.Visibility.Hidden; }
        }

        private void ForegroundReactiveEndType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!WindowInitialized) { return; };
            if (Properties.Settings.Default.ForegroundReactiveEndType == "Defined Colour")
            { ForegroundReactiveEndColor.Visibility = System.Windows.Visibility.Visible; }
            else { ForegroundReactiveEndColor.Visibility = System.Windows.Visibility.Hidden; }
        }

        private void ForegroundReactiveStartColorEdit_Click(object sender, RoutedEventArgs e)
        {
            switch (Properties.Settings.Default.ForegroundReactiveStartType)
            {
                case "Defined Colour":
                    Properties.Settings.Default.ForegroundReactiveSwitchColorStart = OpenColorPicker(Properties.Settings.Default.ForegroundReactiveSwitchColorStart).ToString();
                    break;
                case "Random Colour":
                    EditPageReturnTo = "ForegroundReactiveStart";
                    ColourLabel.Content = "Start Colour";
                    InitialLowerColor = Properties.Settings.Default.ForegroundReactiveColorStartLower;
                    InitialUpperColor = Properties.Settings.Default.ForegroundReactiveColorStartUpper;

                    // Ugly: Assign property values to initialize sliders.
                    // This needs to be replaced with proper binding, somehow.
                    ColorSliders.LowerColor = (Color)ColorConverter.ConvertFromString(InitialLowerColor);
                    ColorSliders.UpperColor = (Color)ColorConverter.ConvertFromString(InitialUpperColor);

                    SetNewColorSlidersBinding("ForegroundReactiveColorStartLower",
                                              "ForegroundReactiveColorStartUpper");

                    GridColor.Visibility = System.Windows.Visibility.Visible;
                    DisableControlsWhileEditing();
                    GridForegroundReactive.IsEnabled = false;
                    break;
            }
        }

        private void ForegroundReactiveEndColorEdit_Click(object sender, RoutedEventArgs e)
        {
            switch (Properties.Settings.Default.ForegroundReactiveEndType)
            {
                case "Defined Colour":
                    Properties.Settings.Default.ForegroundReactiveSwitchColorEnd = OpenColorPicker(Properties.Settings.Default.ForegroundReactiveSwitchColorEnd).ToString();
                    break;
                case "Original Colour":
                    // Do nothing
                    break;
                case "Random Colour":
                    EditPageReturnTo = "ForegroundReactiveEnd";
                    ColourLabel.Content = "End Colour";
                    InitialLowerColor = Properties.Settings.Default.ForegroundReactiveColorEndLower;
                    InitialUpperColor = Properties.Settings.Default.ForegroundReactiveColorEndUpper;

                    // Ugly: Assign property values to initialize sliders.
                    // This needs to be replaced with proper binding, somehow.
                    ColorSliders.LowerColor = (Color)ColorConverter.ConvertFromString(InitialLowerColor);
                    ColorSliders.UpperColor = (Color)ColorConverter.ConvertFromString(InitialUpperColor);

                    SetNewColorSlidersBinding("ForegroundReactiveColorEndLower",
                                              "ForegroundReactiveColorEndUpper");

                    GridColor.Visibility = System.Windows.Visibility.Visible;
                    DisableControlsWhileEditing();
                    GridForegroundReactive.IsEnabled = false;
                    break;
            }
        }

        #endregion Page: ForegroundEdit: Reactive

        private void PageBeingEditedDoneBotton_Click(object sender, RoutedEventArgs e)
        {
            SetWindowLayout("Settings");
        }

        #endregion ForegroundEdit

        #region Color Sliders and Picker

        private void SetNewColorSlidersBinding(string lowerPath, string upperPath)
        {
            Binding ColorSlidersBindingL = new Binding();
            ColorSlidersBindingL.Source = Properties.Settings.Default;
            ColorSlidersBindingL.Path = new PropertyPath(lowerPath);
            ColorSlidersBindingL.Mode = BindingMode.TwoWay;
            ColorSlidersBindingL.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            ColorSlidersBindingL.Converter = new ColorToStringConverter();
            ColorSliders.SetBinding(Controls.RgbSliders.LowerProperty, ColorSlidersBindingL);

            Binding ColorSlidersBindingU = new Binding();
            ColorSlidersBindingU.Source = Properties.Settings.Default;
            ColorSlidersBindingU.Path = new PropertyPath(upperPath);
            ColorSlidersBindingU.Mode = BindingMode.TwoWay;
            ColorSlidersBindingU.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            ColorSlidersBindingU.Converter = new ColorToStringConverter();
            ColorSliders.SetBinding(Controls.RgbSliders.UpperProperty, ColorSlidersBindingU);
        }

        private void AcceptColourButton_Click(object sender, RoutedEventArgs e)
        {
            BindingOperations.ClearAllBindings(ColorSliders);
            CloseColorsAndReEnablePage(EditPageReturnTo);
        }

        private void CancelColourButton_Click(object sender, RoutedEventArgs e)
        {
            BindingOperations.ClearAllBindings(ColorSliders);

            switch (EditPageReturnTo)
            {
                case "ForegroundRandomLightsStart":
                    Properties.Settings.Default.ForegroundRandomLightsColorStartLower = InitialLowerColor;
                    Properties.Settings.Default.ForegroundRandomLightsColorStartUpper = InitialUpperColor;
                    break;
                case "ForegroundRandomLightsEnd":
                    Properties.Settings.Default.ForegroundRandomLightsColorEndLower = InitialLowerColor;
                    Properties.Settings.Default.ForegroundRandomLightsColorEndUpper = InitialUpperColor;
                    break;
                case "ForegroundReactiveStart":
                    Properties.Settings.Default.ForegroundReactiveColorStartLower = InitialLowerColor;
                    Properties.Settings.Default.ForegroundReactiveColorStartUpper = InitialUpperColor;
                    break;
                case "ForegroundReactiveEnd":
                    Properties.Settings.Default.ForegroundReactiveColorEndLower = InitialLowerColor;
                    Properties.Settings.Default.ForegroundReactiveColorEndUpper = InitialUpperColor;
                    break;
            }
            CloseColorsAndReEnablePage(EditPageReturnTo);
        }

        private void CloseColorsAndReEnablePage(string page)
        {
            switch (page)
            {
                case "ForegroundRandomLightsStart": 
                case "ForegroundRandomLightsEnd":
                    GridForegroundRandomLights.IsEnabled = true;
                    break;
                case "ForegroundReactiveStart":
                case "ForegroundReactiveEnd":
                    GridForegroundReactive.IsEnabled = true;
                    break;
            }
            GridPageBeingEdited.IsEnabled = true;
            ForegroundEditButton.IsEnabled = true;
            BackgroundEditButton.IsEnabled = true;
            StaticEditButton.IsEnabled = true;
            GridColor.Visibility = System.Windows.Visibility.Hidden;
        }
        
        private void DisableControlsWhileEditing()
        {
            GridPageBeingEdited.IsEnabled = false;
            ForegroundEditButton.IsEnabled = false;
            BackgroundEditButton.IsEnabled = false;
            StaticEditButton.IsEnabled = false;
        }

        private Color OpenColorPicker(Color inColor)
        {
            ColorPickerStandardDialog dia = new ColorPickerStandardDialog();
            dia.InitialColor = inColor;
            if (dia.ShowDialog() == true)
            { inColor = dia.SelectedColor; }
            return inColor;
        }

        private Color OpenColorPicker(string inColorString)
        {
            Color inColor = (Color)ColorConverter.ConvertFromString(inColorString.ToString());
            ColorPickerStandardDialog dia = new ColorPickerStandardDialog();
            dia.InitialColor = inColor;
            if (dia.ShowDialog() == true)
            { inColor = dia.SelectedColor; }
            return inColor;
        }

        #endregion Color Sliders and Picker

        #endregion Pages
    }

    #region Type Converters
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var dc = (Color)ColorConverter.ConvertFromString(value.ToString());
            return new SolidColorBrush(new Color { A = 255, R = dc.R, G = dc.G, B = dc.B });
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.ToString();
        }
    }

    public class ColorToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (Color)ColorConverter.ConvertFromString(value.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.ToString();
        }
    }
    #endregion Type Converters

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

    public class KeyData
    {
        public string Name;
        public Point[] Coords = new Point[4];
        public int KeyID;
        public ILight KeyColor;
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