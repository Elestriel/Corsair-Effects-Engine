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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Linq;

using NAudio;
using NAudio.CoreAudioApi;

namespace Corsair_Effects_Engine
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Used to clean the GDI Bitmap
        /*
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        */

        // Start with Windows registry key
        RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

        // Application variables
        private const string VersionNumber = "0044";
        private string NewVersionNumber;
        private bool WindowInitialized = false;
        private bool WindowClosing = false;
        public static ImageSelection BackgroundImageSelection = new ImageSelection();

        // Ratio for the loaded image of the keyboard
        private const double KEYBOARD_RATIO = 0.6;

        // Key matrices
        public static KeyData[] keyData = new KeyData[149];
        private Button[] keyboardButtons = new Button[144];
        private Button[] mouseButtons = new Button[5];

        // When editing colour settings, what to return to on accept/cancel
        private static string EditPageReturnTo = "";
        private static string InitialLowerColor;
        private static string InitialUpperColor;

        // Name of the page being edited
        private static string PageBeingEdited;

        // System Tray components
        System.Windows.Forms.NotifyIcon SystemTrayIcon;
        ContextMenu SystemTrayContextMenu;

        // Threads
        Engine newEngine = new Engine();
        Task EngineTask = null;
        System.Windows.Threading.DispatcherTimer defaultDeviceTimer = new System.Windows.Threading.DispatcherTimer();

        #region MainWindow Events

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new CeeDataContext();

            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;

            // Declarations for custom window layout
            this.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, this.OnCloseWindow));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, this.OnMaximizeWindow, this.OnCanResizeWindow));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, this.OnMinimizeWindow, this.OnCanMinimizeWindow));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, this.OnRestoreWindow, this.OnCanResizeWindow));

            // Minimize to Tray stuff
            SystemTrayContextMenu = this.FindResource("TrayContextMenu") as ContextMenu;
            SystemTrayIcon = new System.Windows.Forms.NotifyIcon();
            Stream iconStream = Application.GetResourceStream(new Uri("Resources/CorsairLogoTransparent.ico", UriKind.Relative)).Stream;
            SystemTrayIcon.Icon = new System.Drawing.Icon(iconStream);
            SystemTrayIcon.Visible = true;
            SystemTrayIcon.MouseClick += SystemTrayIcon_MouseClick;
            SystemTrayIcon.DoubleClick +=
                delegate(object sender, EventArgs args)
                {
                    System.Windows.Forms.MouseEventArgs me = (System.Windows.Forms.MouseEventArgs)args;
                    if (me.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        this.Show();
                        this.WindowState = WindowState.Normal;
                    }
                };

            // Launch at startup
            // Check to see the current state (running at startup or not)
            if (rkApp.GetValue("CorsairEffectsEngine") == null)
            {
                // The value doesn't exist, the application is not set to run at startup
                Properties.Settings.Default.OptStartWithWindows = false;
            }
            else
            {
                // The value exists, the application is set to run at startup
                Properties.Settings.Default.OptStartWithWindows = true;
            }
        }

        /// <summary>
        /// Window load tasks.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStatusMessage.NewMsg += UpdateStatusMessage_NewMsg;
            RefreshKeyboardPreview.ShowNewFrame += RefreshKeyboardPreview_ShowNewFrame;

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
            UpdateStatusMessage.NewMessage(0, "Welcome to the Corsair Effects Engine build " + VersionNumber + ".");

            // Check for updates
            Thread UpdateChecker = new Thread(this.CheckForUpdates);
            UpdateChecker.Start();

            // Automatically minimize if the option is set
            if (Properties.Settings.Default.OptStartMinimized)
            { WindowState = WindowState.Minimized; }

            // Set canvas size
            switch (Properties.Settings.Default.KeyboardModel)
            {
                case "K65-RGB":
                    KeyboardMap.CanvasWidth = 76;
                    break;
                case "K70-RGB":
                case "STRAFE":
                case "STRAFE-RGB":
                    KeyboardMap.CanvasWidth = 92;
                    break;
                case "K95-RGB":
                    KeyboardMap.CanvasWidth = 104;
                    break;
            }

            // Start Default Device checker timer
            defaultDeviceTimer.Tick += defaultDeviceTimer_Tick;
            defaultDeviceTimer.Interval = new TimeSpan(0, 0, 2);
            defaultDeviceTimer.Start();
        }

        private void defaultDeviceTimer_Tick(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.OptAudioUseDefaultOutput)
            {
                MMDevice defaultOut = GetDefaultDevice(DataFlow.Render);
                if (defaultOut.FriendlyName != Properties.Settings.Default.AudioOutputDevice)
                { Properties.Settings.Default.AudioOutputDevice = defaultOut.FriendlyName; }
            }
            if (Properties.Settings.Default.OptAudioUseDefaultInput)
            {
                MMDevice defaultIn = GetDefaultDevice(DataFlow.Capture);
                if (defaultIn.FriendlyName != Properties.Settings.Default.AudioOutputDevice)
                { Properties.Settings.Default.AudioInputDevice = defaultIn.FriendlyName; }
            }
        }

        /// <summary>
        /// Window post-load tasks.
        /// </summary>
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            UpdateStatusMessage.NewMessage(0, "Rendered");

            // Load the previous background image, if there is one
            if (LoadBackgroundImage())
            {
                BackgroundImageSelection.OriginalImage = new System.Drawing.Bitmap(Properties.Settings.Default.BackgroundImagePath);
                BackgroundImageSelection.NewImage = new System.Drawing.Bitmap(BackgroundImageSelection.OriginalImage);

                BackgroundImageSelection.Rectangle = Properties.Settings.Default.BackgroundImageRectangle;

                DrawRectangle(BackgroundImageSelection.Rectangle);
                UpdateImagePreview(BackgroundImageSelection.Rectangle);
            }
        }

        private void NewBmp(System.Drawing.Bitmap bmp)
        {
            BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                bmp.GetHbitmap(),
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(bmp.Width, bmp.Height));
            ImageBrush ib = new ImageBrush(bs);
            this.Dispatcher.Invoke(new Action(delegate { GridForegroundSpectro.Background = ib; }));
        }

        /// <summary>
        /// Prevent the window from closing until cleanup is done.
        /// </summary>
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

                // Destroy System Tray icon
                SystemTrayIcon.Dispose();

                // Save settings
                Properties.Settings.Default.Save();

                // Launch CUE
                if (Properties.Settings.Default.OptLaunchCueOnExit)
                {
                    try { System.Diagnostics.Process.Start(Properties.Settings.Default.OptCuePath); }
                    catch { }
                }

                // Close the window
                Application.Current.Windows[0].Close(); 
            }
        }

        /// <summary>
        /// Handle minimization.
        /// </summary>
        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Minimized &&
                Properties.Settings.Default.OptMinimizeToTray == true)
                this.Hide();

            base.OnStateChanged(e);
        }

        /// <summary>
        /// Handle right-click for the system tray icon.
        /// </summary>
        void SystemTrayIcon_MouseClick(object sender, EventArgs e)
        {
            System.Windows.Forms.MouseEventArgs me = (System.Windows.Forms.MouseEventArgs)e;
            if (me.Button == System.Windows.Forms.MouseButtons.Right) 
            { 
                SystemTrayContextMenu.PlacementTarget = sender as Button;
                SystemTrayContextMenu.IsOpen = true;
                this.Activate();
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
        /// Gets the device HID handle.
        /// </summary>
        public void GetDeviceIDs()
        {
            switch (Properties.Settings.Default.KeyboardModel)
            {
                case "K65-RGB": DeviceHID.Keyboard = 0x1B17; break;
                case "K70-RGB": DeviceHID.Keyboard = 0x1B13; break;
                case "K95-RGB": DeviceHID.Keyboard = 0x1B11; break;
                case "STRAFE": DeviceHID.Keyboard = 0x1B15; break;
                case "STRAFE-RGB": DeviceHID.Keyboard = 0x1B20; break;
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
        /// Closes the MainWindow.
        /// </summary>
        private void CloseMainWindow(object sender, RoutedEventArgs e)
        {
            //Application.Current.Shutdown();
            this.Close();
        }

        /// <summary>
        /// Launches the Engine.
        /// </summary>
        private void StartEngine()
        {
            newEngine.PauseEngine = false;
            newEngine.RunEngine = true;
            EngineTask = Task.Run(() => newEngine.Start());
        }

        /// <summary>
        /// Stops the Engine.
        /// </summary>
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

        /// <summary>
        /// Toggles pause/resume for the currently running engine.
        /// </summary>
        private void PauseResumeEngine(object sender, RoutedEventArgs e)
        {
            newEngine.PauseEngine = !newEngine.PauseEngine;
        }

        /// <summary>
        /// Checks update server for an update, and displays a message and button
        /// if there is one available. This is intended to be run as a separate thread.
        /// </summary>
        private void CheckForUpdates()
        {
            string[] VersionCheckData = new string[3];
            int UpdateStatus = 0;

            UpdateStatusMessage.NewMessage(0, "Checking for updates.");

            String URLString = "http://emily-maxwell.com/pages/cee/version.xml";
            try
            {
                XmlTextReader reader = new XmlTextReader(URLString);
                int i = 0;

                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Text: //Display the text in each element.
                            VersionCheckData[i] = reader.Value;
                            i++;
                            break;
                    }
                }
                if (VersionCheckData[0] == VersionNumber) { UpdateStatus = 0; }
                else { UpdateStatus = 1; };
            }
            catch
            {
                UpdateStatus = -1;
            }

            if (UpdateStatus == 1)
            {
                UpdateStatusMessage.NewMessage(0, "Build " + VersionCheckData[0] + " is available.");
                NewVersionNumber = VersionCheckData[0];
                this.UpdateButton.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal, new Action(() => UpdateButton.Visibility = System.Windows.Visibility.Visible));
            }
            else if (UpdateStatus == 0)
            { UpdateStatusMessage.NewMessage(0, "You are running the latest version."); }
            else if (UpdateStatus == -1)
            { UpdateStatusMessage.NewMessage(0, "Could not find the latest version information."); };
        }

        /// <summary>
        /// Starts the self-updater and closes MainWindow.
        /// </summary>
        private void UpdateButton_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(Environment.CurrentDirectory + "\\SelfUpdater.exe",
                    "-CurrentVersion=" + VersionNumber + " -NewVersion=" + NewVersionNumber + " -AccentColour=" + Properties.Settings.Default.OptAccentColor);
            }
            catch
            {
                UpdateStatusMessage.NewMessage(3, "Failed to launch updater.");
                return;
            }

            CloseMainWindow(null, null);
        }

        /// <summary>
        /// Set the registry key when this is checked.
        /// </summary>
        private void CheckStartWithWindows_Checked(object sender, RoutedEventArgs e)
        {
            // Add the value in the registry so that the application runs at startup
            rkApp.SetValue("CorsairEffectsEngine", System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// Remove the registry key when this is unchecked.
        /// </summary>
        private void CheckStartWithWindows_Unchecked(object sender, RoutedEventArgs e)
        {
            // Remove the value from the registry so that the application doesn't start
            rkApp.DeleteValue("CorsairEffectsEngine", false);
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
                    SetGridControlEnabled(true);
                    break;
                case "LogSettings":
                    UpdateStatusMessage.NewMessage(7, "LogSettings");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star);
                    GridContent.Visibility = System.Windows.Visibility.Visible;
                    GridLeftLog.Visibility = System.Windows.Visibility.Visible;
                    GridRightLog.Visibility = System.Windows.Visibility.Visible;
                    SetGridControlEnabled(true);
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
                    SetGridControlEnabled(true);
                    break;
                #endregion Settings
                #region Mouse
                case "Mouse":
                    UpdateStatusMessage.NewMessage(7, "Mouse");
                    ContentLeft.Width = new GridLength(500, GridUnitType.Star);
                    ContentRight.Width = new GridLength(200, GridUnitType.Star); 
                    GridKeyboard.Visibility = System.Windows.Visibility.Visible;
                    KeyboardImage.Visibility = System.Windows.Visibility.Visible;
                    SetGridControlEnabled(true);

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
                    SetGridControlEnabled(true);

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

                    SetGridControlEnabled(false);

                    PageBeingEdited = mode2;
                    PageBeingEditedLabel.Content = "Currently Editing: " + PageBeingEdited;

                    switch (mode2)
                    {
                        case "Spectrograph":
                            ForegroundSpectroStyle_SelectionChanged(null, null);
                            GridForegroundSpectro.Visibility = System.Windows.Visibility.Visible;
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
                        case "CPU Usage Bar":
                            GridForegroundCpuBar.Visibility = System.Windows.Visibility.Visible;
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
                    GridRightSettings.Visibility = System.Windows.Visibility.Visible;
                    
                    SetGridControlEnabled(false);

                    PageBeingEdited = mode2;
                    PageBeingEditedLabel.Content = "Currently Editing: " + PageBeingEdited;

                    switch (mode2)
                    {
                        case "Solid":
                            GridBackgroundSolid.Visibility = System.Windows.Visibility.Visible;
                            break;
                        case "Breathe":
                            GridBackgroundBreathe.Visibility = System.Windows.Visibility.Visible;
                            break;
                        case "Spectrum Cycle":
                            GridBackground.Visibility = System.Windows.Visibility.Visible;
                            GridBackgroundSpectrum.Visibility = System.Windows.Visibility.Visible;
                            break;
                        case "Rainbow":
                            GridBackground.Visibility = System.Windows.Visibility.Visible;
                            GridBackgroundRainbow.Visibility = System.Windows.Visibility.Visible;
                            break;
                        case "Image":
                            GridBackgroundImage.Visibility = System.Windows.Visibility.Visible;
                            break;
                        case "CPU Usage":
                            GridBackgroundCpu.Visibility = System.Windows.Visibility.Visible;
                            break;
                        case "Windows Accent":
                            GridBackgroundWinAccent.Visibility = System.Windows.Visibility.Visible;
                            break;
                    }
                    break;
                #endregion Foreground
                #region Static
                case "StaticEdit":
                    //GridKeyboard.Visibility = System.Windows.Visibility.Visible;
                    //KeyboardImage.Visibility = System.Windows.Visibility.Visible;
                    break;
                #endregion Foreground
            }
        }

        private void SetGridControlEnabled(bool enabled)
        {
            ForegroundEditButton.IsEnabled = enabled;
            ForegroundComboBox.IsEnabled = enabled;

            BackgroundEditButton.IsEnabled = enabled;
            BackgroundComboBox.IsEnabled = enabled;

            StaticEditButton.IsEnabled = enabled;
            StaticComboBox.IsEnabled = enabled;

            GridControlsButtons.IsEnabled = enabled;
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
            GridForegroundSpectro.Visibility = System.Windows.Visibility.Hidden;
            GridForegroundRandomLights.Visibility = System.Windows.Visibility.Hidden;
            GridForegroundReactive.Visibility = System.Windows.Visibility.Hidden;
            GridForegroundHeatmap.Visibility = System.Windows.Visibility.Hidden;
            GridForegroundCpuBar.Visibility = System.Windows.Visibility.Hidden;

            // Edit: Background
            GridBackground.Visibility = System.Windows.Visibility.Hidden;
            GridBackgroundSolid.Visibility = System.Windows.Visibility.Hidden;
            GridBackgroundBreathe.Visibility = System.Windows.Visibility.Hidden;
            GridBackgroundSpectrum.Visibility = System.Windows.Visibility.Hidden;
            GridBackgroundRainbow.Visibility = System.Windows.Visibility.Hidden;
            GridBackgroundImage.Visibility = System.Windows.Visibility.Hidden;
            GridBackgroundCpu.Visibility = System.Windows.Visibility.Hidden;
            GridBackgroundWinAccent.Visibility = System.Windows.Visibility.Hidden;

            // Full Keyboard
            GridKeyboard.Visibility = System.Windows.Visibility.Hidden;
            KeyboardImage.Visibility = System.Windows.Visibility.Hidden;
        }

        #endregion Layout Configurations

        #region Settings Events

        public MMDevice GetDefaultDevice(DataFlow direction)
        {
            MMDeviceEnumerator deviceEnum = new MMDeviceEnumerator();
            return deviceEnum.GetDefaultAudioEndpoint(direction, Role.Multimedia);
        }

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

        private void UseDefaultOutputDevice_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AudioOutputDevice = GetDefaultDevice(DataFlow.Render).FriendlyName;
        }

        private void AudioOutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Properties.Settings.Default.ForegroundEffect == "Spectrograph" &&
                Properties.Settings.Default.ForegroundEffectEnabled == true)
            { newEngine.RestartEngine = true; }
        }

        private void UseDefaultInputDevice_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AudioInputDevice = GetDefaultDevice(DataFlow.Capture).FriendlyName;
        }

        private void AudioInputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            newEngine.RestartEngine = true;
        }

        private void AudioOutputDeviceRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            newEngine.RestartEngine = true;
        }

        private void AudioInputDeviceRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            newEngine.RestartEngine = true;
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
            //if (Model == "None" || Region == "None") { DrawButtonsOnKeyboard(Clear: true); };
            
            newEngine.RestartEngine = true;
        }



        #endregion Live Keyboard Preview

        #region Pages

        #region Page: ForegroundEdit

        #region Page: ForegroundEdit: Spectro

        private void ForegroundSpectroStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (Properties.Settings.Default.ForegroundSpectroStyle == "Gradient Horizontal" ||
                    Properties.Settings.Default.ForegroundSpectroStyle == "Gradient Vertical")
                {
                    fftColorPicker.Visibility = System.Windows.Visibility.Visible;
                    fftColorPicker_Gradient.Visibility = System.Windows.Visibility.Visible;
                    fftRainbowBrightness.Visibility = System.Windows.Visibility.Hidden;
                    fftRainbowBrightnessLabel.Visibility = System.Windows.Visibility.Hidden;
                    SpectroRainbowDirection.Visibility = System.Windows.Visibility.Hidden;
                    SpectroRainbowDirectionLabel.Visibility = System.Windows.Visibility.Hidden;
                    GridForegroundSpectroPerRow.Visibility = System.Windows.Visibility.Hidden;
                    SpectroCycleLabel.Visibility = System.Windows.Visibility.Hidden;
                    SpectroCycleUpDown.Visibility = System.Windows.Visibility.Hidden;
                }
                else if (Properties.Settings.Default.ForegroundSpectroStyle == "Rainbow")
                {
                    fftColorPicker.Visibility = System.Windows.Visibility.Hidden;
                    fftColorPicker_Gradient.Visibility = System.Windows.Visibility.Hidden;
                    fftRainbowBrightness.Visibility = System.Windows.Visibility.Visible;
                    fftRainbowBrightnessLabel.Visibility = System.Windows.Visibility.Visible;
                    SpectroRainbowDirection.Visibility = System.Windows.Visibility.Visible;
                    SpectroRainbowDirectionLabel.Visibility = System.Windows.Visibility.Visible;
                    GridForegroundSpectroPerRow.Visibility = System.Windows.Visibility.Hidden;
                    SpectroCycleLabel.Visibility = System.Windows.Visibility.Visible;
                    SpectroCycleUpDown.Visibility = System.Windows.Visibility.Visible;
                }
                else if (Properties.Settings.Default.ForegroundSpectroStyle == "Solid")
                {
                    fftColorPicker.Visibility = System.Windows.Visibility.Visible;
                    fftColorPicker_Gradient.Visibility = System.Windows.Visibility.Hidden;
                    fftRainbowBrightness.Visibility = System.Windows.Visibility.Hidden;
                    fftRainbowBrightnessLabel.Visibility = System.Windows.Visibility.Hidden;
                    SpectroRainbowDirection.Visibility = System.Windows.Visibility.Hidden;
                    SpectroRainbowDirectionLabel.Visibility = System.Windows.Visibility.Hidden;
                    GridForegroundSpectroPerRow.Visibility = System.Windows.Visibility.Hidden;
                    SpectroCycleLabel.Visibility = System.Windows.Visibility.Hidden;
                    SpectroCycleUpDown.Visibility = System.Windows.Visibility.Hidden;
                }
                else if  (Properties.Settings.Default.ForegroundSpectroStyle == "Spectrum Cycle")
                {
                    fftColorPicker.Visibility = System.Windows.Visibility.Hidden;
                    fftColorPicker_Gradient.Visibility = System.Windows.Visibility.Hidden;
                    fftRainbowBrightness.Visibility = System.Windows.Visibility.Visible;
                    fftRainbowBrightnessLabel.Visibility = System.Windows.Visibility.Visible;
                    SpectroRainbowDirection.Visibility = System.Windows.Visibility.Hidden;
                    SpectroRainbowDirectionLabel.Visibility = System.Windows.Visibility.Hidden;
                    GridForegroundSpectroPerRow.Visibility = System.Windows.Visibility.Hidden;
                    SpectroCycleLabel.Visibility = System.Windows.Visibility.Visible;
                    SpectroCycleUpDown.Visibility = System.Windows.Visibility.Visible;
                }
                else if (Properties.Settings.Default.ForegroundSpectroStyle == "Defined Rows")
                {
                    fftColorPicker.Visibility = System.Windows.Visibility.Hidden;
                    fftColorPicker_Gradient.Visibility = System.Windows.Visibility.Hidden;
                    fftRainbowBrightness.Visibility = System.Windows.Visibility.Hidden;
                    fftRainbowBrightnessLabel.Visibility = System.Windows.Visibility.Hidden;
                    SpectroRainbowDirection.Visibility = System.Windows.Visibility.Hidden;
                    SpectroRainbowDirectionLabel.Visibility = System.Windows.Visibility.Hidden;
                    GridForegroundSpectroPerRow.Visibility = System.Windows.Visibility.Visible;
                    SpectroCycleLabel.Visibility = System.Windows.Visibility.Hidden;
                    SpectroCycleUpDown.Visibility = System.Windows.Visibility.Hidden;
                }
            }
            catch { }
        }

        private void ForegroundCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (Properties.Settings.Default.ForegroundEffect == "Spectrograph")
            {
                newEngine.RestartEngine = true;
            }
        }

        private void ApplySpectroChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (Properties.Settings.Default.ForegroundEffect == "Spectrograph")
            {
                newEngine.RestartEngine = true;
            }
        }

        private void dbMinUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Properties.Settings.Default.FftAmplitudeMin > Properties.Settings.Default.FftAmplitudeMax - 10)
            {
                if (Properties.Settings.Default.FftAmplitudeMin <= -10)
                { Properties.Settings.Default.FftAmplitudeMax = Properties.Settings.Default.FftAmplitudeMin + 10; }
                else
                { Properties.Settings.Default.FftAmplitudeMin = -10; }
            }
        }

        private void dbMaxUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Properties.Settings.Default.FftAmplitudeMax < Properties.Settings.Default.FftAmplitudeMin + 10)
            {
                if (Properties.Settings.Default.FftAmplitudeMax >= -110)
                { Properties.Settings.Default.FftAmplitudeMin = Properties.Settings.Default.FftAmplitudeMax - 10; }
                else
                { Properties.Settings.Default.FftAmplitudeMax = -110; }
            }
        }

        private void freqMinUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Properties.Settings.Default.FftFrequencyMin > Properties.Settings.Default.FftFrequencyMax - 100)
            {
                if (Properties.Settings.Default.FftFrequencyMin <= 19900)
                { Properties.Settings.Default.FftFrequencyMax = Properties.Settings.Default.FftFrequencyMin + 100; }
                else
                { Properties.Settings.Default.FftFrequencyMin = 19900; }
            }
        }

        private void freqMaxUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Properties.Settings.Default.FftFrequencyMax < Properties.Settings.Default.FftFrequencyMin + 100)
            {
                if (Properties.Settings.Default.FftFrequencyMax >= 100)
                { Properties.Settings.Default.FftFrequencyMin = Properties.Settings.Default.FftFrequencyMax - 100; }
                else
                { Properties.Settings.Default.FftFrequencyMax = 100; }
            }
        }

        #endregion Page: ForegroundEdit: Spectro

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
            switch (Properties.Settings.Default.ForegroundRandomLightsStartType)
            {
            case "Defined Colour":
                ForegroundRandomLightsStartColor.Visibility = System.Windows.Visibility.Visible;
                ForegroundRandomLightsStartColorEdit.Visibility = System.Windows.Visibility.Hidden;
                break;
            case "Random Colour":
                ForegroundRandomLightsStartColor.Visibility = System.Windows.Visibility.Hidden;
                ForegroundRandomLightsStartColorEdit.Visibility = System.Windows.Visibility.Visible;
                break;
            }
        }

        private void ForegroundRandomLightsEndType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!WindowInitialized) { return; };
            switch (Properties.Settings.Default.ForegroundRandomLightsEndType)
            {
                case "Defined Colour":
                    ForegroundRandomLightsEndColor.Visibility = System.Windows.Visibility.Visible;
                    ForegroundRandomLightsEndColorEdit.Visibility = System.Windows.Visibility.Hidden;
                    break;
                case "Random Colour":
                    ForegroundRandomLightsEndColor.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundRandomLightsEndColorEdit.Visibility = System.Windows.Visibility.Visible;
                    break;
                case "None":
                    ForegroundRandomLightsEndColor.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundRandomLightsEndColorEdit.Visibility = System.Windows.Visibility.Hidden;
                    break;
            }
        }

        private void ForegroundRandomLightsStartColorEdit_Click(object sender, RoutedEventArgs e)
        {
            switch (Properties.Settings.Default.ForegroundRandomLightsStartType)
            {
                case "Defined Colour":
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
            switch (Properties.Settings.Default.ForegroundReactiveStartType) {
                case "Defined Colour":
                    ForegroundReactiveStartColor.Visibility = System.Windows.Visibility.Visible;
                    ForegroundReactiveStartColorEdit.Visibility = System.Windows.Visibility.Hidden;
                    break;
                case "Random Colour":
                    ForegroundReactiveStartColor.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundReactiveStartColorEdit.Visibility = System.Windows.Visibility.Visible;
                    break;
            }
        }

        private void ForegroundReactiveEndType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!WindowInitialized) { return; };
            switch (Properties.Settings.Default.ForegroundReactiveEndType) {
                case "Defined Colour":
                    ForegroundReactiveEndColor.Visibility = System.Windows.Visibility.Visible;
                    ForegroundReactiveEndColorEdit.Visibility = System.Windows.Visibility.Hidden;
                    break;
                case "Random Colour":
                    ForegroundReactiveEndColor.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundReactiveEndColorEdit.Visibility = System.Windows.Visibility.Visible;
                    break;
                case "None":
                    ForegroundReactiveEndColor.Visibility = System.Windows.Visibility.Hidden;
                    ForegroundReactiveEndColorEdit.Visibility = System.Windows.Visibility.Hidden;
                    break;
            }
        }

        private void ForegroundReactiveStartColorEdit_Click(object sender, RoutedEventArgs e)
        {
            switch (Properties.Settings.Default.ForegroundReactiveStartType)
            {
                case "Defined Colour":
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

        #region Page: ForegroundEdit: Heatmap

        private void HeatmapResetCountButton_Click(object sender, RoutedEventArgs e)
        {
            newEngine.HeatmapHighestStrikeCount = 0;
            for (int i = 0; i < 149; i++)
            {
                newEngine.HeatmapStrikeCount[i] = 0;
            }
            newEngine.ClearAllKeys();
        }

        #endregion Page: ForegroundEdit: Heatmap

        private void PageBeingEditedDoneBotton_Click(object sender, RoutedEventArgs e)
        {
            SetWindowLayout("Settings");

            // Save settings
            Properties.Settings.Default.Save();
        }

        #endregion ForegroundEdit

        #region Page: BackgroundEdit

        #region Page: BackgroundEdit: Image

        private void BackgroundImageBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.InitialDirectory = "%USERPROFILE%";
            openFileDialog.Filter = "Bitmap|*.bmp|JPEG Image|*.jpg;*.jpeg|PNG Image|*.png|GIF Image|*.gif|All Images|*.bmp;*.gif;*.jpg;*.jpeg;*.png|All Files (*.*)|*.*";
            openFileDialog.FilterIndex = 5;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == true)
            {
                Properties.Settings.Default.BackgroundImagePath = openFileDialog.FileName;
                LoadBackgroundImage();
            };
        }

        private bool LoadBackgroundImage()
        {
            if (!File.Exists(Properties.Settings.Default.BackgroundImagePath)) { return false; }

            try
            {
                BitmapImage newImage = new BitmapImage(new Uri(Properties.Settings.Default.BackgroundImagePath));
                double aspectRatio = newImage.Width / newImage.Height;
                int originalCanvasHeight = 240;
                int originalCanvasWidth = 459;
                if (aspectRatio > 1.9125)
                {
                    BackgroundImageCanvas.Height = (int)(originalCanvasWidth * aspectRatio);
                    BackgroundImageCanvas.Width = originalCanvasWidth;
                }
                else
                {
                    BackgroundImageCanvas.Width = (int)(originalCanvasHeight * aspectRatio);
                    BackgroundImageCanvas.Height = originalCanvasHeight;
                }
                ImageBrush newBrush = new ImageBrush(newImage);
                BackgroundImageCanvas.Background = newBrush;
            }
            catch { return false; }
            return true;
        }

        private void BackgroundImageCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            BackgroundImageSelection.IsEditing = true;

            // Make a Bitmap to display the selection rectangle.
            BackgroundImageSelection.OriginalImage = new System.Drawing.Bitmap(Properties.Settings.Default.BackgroundImagePath);

            Point p = e.GetPosition(BackgroundImageCanvas);
            
            int relX = (int)(p.X * (BackgroundImageSelection.OriginalImage.Width / BackgroundImageCanvas.Width));
            int relY = (int)(p.Y * (BackgroundImageSelection.OriginalImage.Height / BackgroundImageCanvas.Height));
            BackgroundImageSelection.MouseDownX = relX;
            BackgroundImageSelection.MouseDownY = relY;

            if (relX > BackgroundImageSelection.X &&
                relX < (BackgroundImageSelection.X + BackgroundImageSelection.W) &&
                relY > BackgroundImageSelection.Y &&
                relY < (BackgroundImageSelection.Y + BackgroundImageSelection.H))
            { BackgroundImageSelection.IsMoveMode = true; }
            else
            { BackgroundImageSelection.IsMoveMode = false; }
        }

        private void BackgroundImageCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            BackgroundImageSelection.IsEditing = false;
        }

        private void BackgroundImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            /*
            if (BackgroundImageSelection.IsEditing)
            {
                BackgroundImageSelection.NewImage = new System.Drawing.Bitmap(BackgroundImageSelection.OriginalImage);
                Point p = e.GetPosition(BackgroundImageCanvas);
                System.Drawing.Rectangle rect;

                if (BackgroundImageSelection.IsMoveMode) 
                {
                    int relX = (int)(BackgroundImageSelection.MouseDownX - p.X * (BackgroundImageSelection.OriginalImage.Width / BackgroundImageCanvas.Width));
                    int relY = (int)(BackgroundImageSelection.MouseDownY - p.Y * (BackgroundImageSelection.OriginalImage.Height / BackgroundImageCanvas.Height));
                    int newX = (int)(BackgroundImageSelection.X - relX);
                    int newY = (int)(BackgroundImageSelection.Y - relY);
                    int newW = (int)(BackgroundImageSelection.W);
                    int newH = (int)(BackgroundImageSelection.H);

                    if (newX < 0) { newX = 0; }
                    if (newY < 0) { newY = 0; }
                    if (newX > BackgroundImageSelection.OriginalImage.Width - newW) { newX = BackgroundImageSelection.OriginalImage.Width - newW; }
                    if (newY > BackgroundImageSelection.OriginalImage.Height - newH) { newY = BackgroundImageSelection.OriginalImage.Height - newH; }
                    
                    rect = new System.Drawing.Rectangle(newX, newY, newW, newH);
                }
                else
                {
                    int X = (int)BackgroundImageSelection.MouseDownX;
                    int Y = (int)BackgroundImageSelection.MouseDownY;

                    // Save the new point.
                    int W = (int)(p.X * (BackgroundImageSelection.NewImage.Width / BackgroundImageCanvas.Width));
                    int H = (int)(p.Y * (BackgroundImageSelection.NewImage.Height / BackgroundImageCanvas.Height));

                    int PX = Math.Min(X, W);
                    int PY = Math.Min(Y, H);
                    int PW;
                    if (W > X) { PW = W; } else { PW = X; };
                    PW = Math.Abs(W - X);
                    int PH;
                    if (H > Y) { PH = H; } else { PH = Y; };
                    PH = Math.Abs(H - Y);

                    BackgroundImageSelection.X = PX;
                    BackgroundImageSelection.Y = PY;
                    BackgroundImageSelection.W = PW;
                    BackgroundImageSelection.H = PH;

                    rect = new System.Drawing.Rectangle(PX, PY, PW, PH);
                }
                DrawRectangle(rect);
                BackgroundImageSelection.NewImage.Dispose();
            }
             * */
        }

        private void DrawRectangle(System.Drawing.Rectangle rect)
        {
            /*
            // Draw the rectangle.
            using (System.Drawing.Graphics gr = System.Drawing.Graphics.FromImage(BackgroundImageSelection.NewImage))
            {
                Properties.Settings.Default.BackgroundImageRectangle = rect;
                System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(127, 255, 255, 255));
                gr.FillRectangle(brush, rect);
                gr.Dispose();
            }
            
            IntPtr hBitmap = BackgroundImageSelection.NewImage.GetHbitmap();
            try
            {
                
                ImageSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                ImageBrush newBrush = new ImageBrush(source);

                BackgroundImageCanvas.Background = newBrush;
                UpdateImagePreview(rect);
            }
            finally
            { DeleteObject(hBitmap); }
            */
        }

        private void UpdateImagePreview(System.Drawing.Rectangle rect)
        {
            /*
            if (rect.Width < 1 && rect.Height < 1) { return; }
            if (KeyboardMap.CanvasWidth == 0) { return; }
            // Adjust size of preview
            if (KeyboardMap.CanvasWidth > 0 && KeyboardMap.CanvasWidth != BackgroundImageResizedCanvas.Width)
            { BackgroundImageResizedCanvas.Width = KeyboardMap.CanvasWidth; };

            // Resize image
            System.Drawing.Rectangle destRect = new System.Drawing.Rectangle(0, 0, KeyboardMap.CanvasWidth, 7);
            System.Drawing.Bitmap destImage = new System.Drawing.Bitmap(KeyboardMap.CanvasWidth, 7);

            destImage.SetResolution(BackgroundImageSelection.OriginalImage.HorizontalResolution, BackgroundImageSelection.OriginalImage.VerticalResolution);

            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using (System.Drawing.Imaging.ImageAttributes wrapMode = new System.Drawing.Imaging.ImageAttributes())
                {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(BackgroundImageSelection.OriginalImage, destRect, rect, System.Drawing.GraphicsUnit.Pixel);
                }
                graphics.Dispose();
            }

            IntPtr hBitmap = destImage.GetHbitmap();
            try
            {
                ImageSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                ImageBrush newBrush = new ImageBrush(source);
                BackgroundImageResizedCanvas.Background = newBrush;
                BackgroundImageSelection.OutputImage = destImage;
            }
            finally
            { DeleteObject(hBitmap); }

            destImage.Dispose();
             * */
        }
        
        #endregion Page: BackgroundEdit: Image

        #endregion Page: BackgroundEdit

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

        #endregion Color Sliders and Picker

        private void DonateButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://emily-maxwell.com/?page=donate");
        }
        
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

    public class ColorToHexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var hexCode = System.Convert.ToString(value);
            if (string.IsNullOrEmpty(hexCode))
                return null;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexCode);
                return color;
            }
            catch
            {
                return null;
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                return value.ToString();
            }
            catch
            {
                return null;
            }
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

    public class BoolSwap : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
    #endregion Type Converters

    #region Data Classes

    public static class KeyboardMap
    {
        public static byte[] Positions;
        public static float[] Sizes;
        public static int CanvasWidth = 3;
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

    public class ImageSelection
    {
        public bool IsEditing;
        public bool IsMoveMode;
        public double X;
        public double Y;
        public double W;
        public double H;
        public double MouseDownX;
        public double MouseDownY;
        public System.Drawing.Bitmap OriginalImage;
        public System.Drawing.Bitmap NewImage;
        public System.Drawing.Bitmap OutputImage;

        public System.Drawing.Rectangle Rectangle
        {
            get { return new System.Drawing.Rectangle((int)X, (int)Y, (int)W, (int)H); }
            set
            {
                X = value.X;
                Y = value.Y;
                W = value.Width;
                H = value.Height; 
            }
        }
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

    class GifImage : Image
    {
        private bool _isInitialized;
        private GifBitmapDecoder _gifDecoder;
        private Int32Animation _animation;

        public int FrameIndex
        {
            get { return (int)GetValue(FrameIndexProperty); }
            set { SetValue(FrameIndexProperty, value); }
        }

        private void Initialize()
        {
            _gifDecoder = new GifBitmapDecoder(new Uri("pack://application:,,," + this.GifSource), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            _animation = new Int32Animation(0, _gifDecoder.Frames.Count - 1, new Duration(new TimeSpan(0, 0, 0, _gifDecoder.Frames.Count / 10, (int)((_gifDecoder.Frames.Count / 10.0 - _gifDecoder.Frames.Count / 10) * 1000))));
            _animation.RepeatBehavior = RepeatBehavior.Forever;
            this.Source = _gifDecoder.Frames[0];

            _isInitialized = true;
        }

        static GifImage()
        {
            VisibilityProperty.OverrideMetadata(typeof(GifImage),
                new FrameworkPropertyMetadata(VisibilityPropertyChanged));
        }

        private static void VisibilityPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if ((Visibility)e.NewValue == Visibility.Visible)
            {
                ((GifImage)sender).StartAnimation();
            }
            else
            {
                ((GifImage)sender).StopAnimation();
            }
        }

        public static readonly DependencyProperty FrameIndexProperty =
            DependencyProperty.Register("FrameIndex", typeof(int), typeof(GifImage), new UIPropertyMetadata(0, new PropertyChangedCallback(ChangingFrameIndex)));

        static void ChangingFrameIndex(DependencyObject obj, DependencyPropertyChangedEventArgs ev)
        {
            var gifImage = obj as GifImage;
            gifImage.Source = gifImage._gifDecoder.Frames[(int)ev.NewValue];
        }

        /// <summary>
        /// Defines whether the animation starts on it's own
        /// </summary>
        public bool AutoStart
        {
            get { return (bool)GetValue(AutoStartProperty); }
            set { SetValue(AutoStartProperty, value); }
        }

        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.Register("AutoStart", typeof(bool), typeof(GifImage), new UIPropertyMetadata(false, AutoStartPropertyChanged));

        private static void AutoStartPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                (sender as GifImage).StartAnimation();
        }

        public string GifSource
        {
            get { return (string)GetValue(GifSourceProperty); }
            set { SetValue(GifSourceProperty, value); }
        }

        public static readonly DependencyProperty GifSourceProperty =
            DependencyProperty.Register("GifSource", typeof(string), typeof(GifImage), new UIPropertyMetadata(string.Empty, GifSourcePropertyChanged));

        private static void GifSourcePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            (sender as GifImage).Initialize();
        }

        /// <summary>
        /// Starts the animation
        /// </summary>
        public void StartAnimation()
        {
            if (!_isInitialized)
                this.Initialize();

            BeginAnimation(FrameIndexProperty, _animation);
        }

        /// <summary>
        /// Stops the animation
        /// </summary>
        public void StopAnimation()
        {
            BeginAnimation(FrameIndexProperty, null);
        }
    }

    #endregion Class Extensions

    #region Thread Delegates

    public delegate void AddStatusMessageDelegate(int messageType, string messageText);
    /// <summary>
    /// Sends a new message to the log and debug output.
    /// </summary>
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