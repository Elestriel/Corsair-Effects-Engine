using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Corsair_Effects_Engine
{
    public static class Engine
    {
        public static bool PauseEngine = false;
        public static bool RunEngine = false;
        public static bool RestartEngine = false;
        public static bool EngineIsPaused = false;

        private static IntPtr KeyboardPointer;
        private static IntPtr MousePointer;

        private static KeyData[] Keys = MainWindow.keyData;
        
        public static void Start()
        {
            UpdateStatusMessage.NewMessage(5, "Engine started.");

            EngineComponents.InitDevices DeviceInit = new EngineComponents.InitDevices();
            EngineComponents.DeviceOutput Output = new EngineComponents.DeviceOutput();

            Random rnd = new Random();

            while (RunEngine)
            {
                UpdateStatusMessage.NewMessage(5, "Initializing Keyboard.");
                // Initialize keyboard
                KeyboardPointer = DeviceInit.GetKeyboardPointer();

                // Initialize mouse
                MousePointer = DeviceInit.GetMousePointer();

                while (!PauseEngine && RunEngine && !RestartEngine)
                {
                    EngineIsPaused = false;
                    // Use 'rendered' flag to occlude following layers
                    // Render static layer

                    // Render foreground layer

                    // Render background layer

                    // Test Render
                    int keyLight = rnd.Next(0, 149);
                    if (Keys[keyLight].KeyColor.EffectInProgress == false)
                    {
                        Keys[keyLight].KeyColor = new LightFade(startColor: Color.FromRgb(0, 255, 0),
                                                        endColor: Color.FromRgb(0, 0, 0),
                                                        solidDuration: 2000,
                                                        totalDuration: 3000);
                    }


                    // Output frame to keyboard preview

                    // Output frame to devices
                    Output.UpdateKeyboard(KeyboardPointer, Keys);
                    Output.UpdateMouse(MousePointer, Keys);

                    //UpdateStatusMessage.NewMessage(5, "Engine MainLoop");
                    Thread.Sleep(15);
                }
                if (RestartEngine)
                {
                    UpdateStatusMessage.NewMessage(5, "Reinitializing.");
                    RestartEngine = false;
                }
                while (PauseEngine && RunEngine)
                {
                    EngineIsPaused = true;
                    UpdateStatusMessage.NewMessage(5, "Engine is asleep...");
                    Thread.Sleep(1000);
                }
            }
            if (Properties.Settings.Default.OptRestoreLightingOnExit)
            {
                UpdateStatusMessage.NewMessage(5, "Restoring lighting.");
                Output.RestoreKeyboard(KeyboardPointer);
                Output.RestoreMouse(MousePointer);
            }
            UpdateStatusMessage.NewMessage(5, "Engine is shutting down.");
        }
    }
}
