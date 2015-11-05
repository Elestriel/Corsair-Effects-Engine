using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Corsair_Effects_Engine.EngineComponents
{
    public class InitDevices
    {
        #region pInvoke Imports
        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetFeature(IntPtr HidDeviceObject, ref Byte lpReportBuffer, int ReportBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        static public extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, ref uint lpNumberOfBytesWritten, IntPtr ipOverlapped);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SetupDiGetClassDevs(           // 1st form using a ClassGUID only, with null Enumerator
           ref Guid ClassGuid,
           IntPtr Enumerator,
           IntPtr hwndParent,
           int Flags
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        static extern int CM_Get_Device_ID(
           UInt32 dnDevInst,
           IntPtr buffer,
           int bufferLen,
           int flags
        );

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern Boolean SetupDiEnumDeviceInterfaces(
           IntPtr hDevInfo,
           ref SP_DEVINFO_DATA devInfo,
           ref Guid interfaceClassGuid,
           UInt32 memberIndex,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData
        );

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr hDevInfo,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           ref SP_DEVINFO_DATA deviceInfoData
        );

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr hDevInfo,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           IntPtr requiredSize,                     // Allow null
           IntPtr deviceInfoData                    // Allow null
        );

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern Boolean SetupDiGetDeviceInterfaceDetail(
           IntPtr hDevInfo,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           IntPtr deviceInterfaceDetailData,        // Allow null
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           IntPtr deviceInfoData                    // Allow null
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList
        (
             IntPtr DeviceInfoSet
        );

        #endregion

        #region Types to support pInvoke methods

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid classGuid;
            public uint devInst;
            public IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid interfaceClassGuid;
            public uint flags;
            private UIntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public uint cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = BUFFER_SIZE)]
            public string DevicePath;
        }

        #endregion

        #region Flags to support pInvoke methods

        const Int64 INVALID_HANDLE_VALUE = -1;

        const int DIGCF_DEFAULT = 0x1;
        const int DIGCF_PRESENT = 0x2;
        const int DIGCF_ALLCLASSES = 0x4;
        const int DIGCF_PROFILE = 0x8;
        const int DIGCF_DEVICEINTERFACE = 0x10;

        // Used for CreateFile
        public const short FILE_ATTRIBUTE_NORMAL = 0x80;

        //public const short INVALID_HANDLE_VALUE = -1;
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint CREATE_NEW = 1;
        public const uint CREATE_ALWAYS = 2;
        public const uint OPEN_EXISTING = 3;

        // Used for CreateFile
        public const uint FILE_SHARE_NONE = 0x00;
        public const uint FILE_SHARE_READ = 0x01;
        public const uint FILE_SHARE_WRITE = 0x02;
        public const uint FILE_SHARE_DELETE = 0x04;

        // Used for CreateFile
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        static Guid GUID_DEVINTERFACE_HID = new Guid(0x4D1E55B2, 0xF16F, 0x11CF, 0x88, 0xCB, 0x00, 0x11, 0x11, 0x00, 0x00, 0x30);

        const int BUFFER_SIZE = 128;
        const int MAX_DEVICE_ID_LEN = 200;

        #endregion

        private IntPtr UsbDevice;
        string[] keyboardIDs;
        string[] keyboardNames;
        string[] keyboardPositionMaps;
        string[] keyboardSizeMaps;

        public IntPtr GetKeyboardPointer()
        {
            if (InitDevice(DeviceHID.Keyboard, Properties.Settings.Default.KeyboardModel, "keyboard") == 0) { return UsbDevice; }
            else { return IntPtr.Zero; };
        }

        public IntPtr GetMousePointer()
        {
            if (InitDevice(DeviceHID.Mouse, Properties.Settings.Default.MouseModel, "mouse") == 0) { return UsbDevice; }
            else { return IntPtr.Zero; };
        }

        private int InitDevice(uint DeviceID, string DeviceName, string DeviceType)
        {
            UpdateStatusMessage.NewMessage(4, "Searching for " + DeviceName + " (" + DeviceID.ToString("X") + ").");

            this.UsbDevice = this.GetDeviceHandle(0x1B1C, DeviceID, 0x3);

            if (this.UsbDevice == IntPtr.Zero)
            {
                UpdateStatusMessage.NewMessage(3, FirstLetterToUpper(DeviceType) + " not found.");
                return 1;
            }

            UpdateStatusMessage.NewMessage(4, FirstLetterToUpper(DeviceType) + " found.");

            if (DeviceType == "keyboard")
            {
                // Construct XY lookup table
                if (InitiateLookupTable() == false)
                {
                    UpdateStatusMessage.NewMessage(3, "An error occurred when attempting to initiate the " + DeviceType + ".");
                    return 1;
                }
            }
            return 0;
        }

        /// <summary>
        /// C Code by http://www.reddit.com/user/chrisgzy
        /// Converted to C# by http://www.reddit.com/user/billism
        /// </summary>
        private IntPtr GetDeviceHandle(uint uiVID, uint uiPID, uint uiMI)
        {
            IntPtr deviceInfo = SetupDiGetClassDevs(ref GUID_DEVINTERFACE_HID, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (deviceInfo.ToInt64() == INVALID_HANDLE_VALUE)
            {
                return IntPtr.Zero;
            }

            IntPtr returnPointer = IntPtr.Zero;

            SP_DEVINFO_DATA deviceData = new SP_DEVINFO_DATA();
            deviceData.cbSize = (uint)Marshal.SizeOf(deviceData);

            for (uint i = 0; SetupDiEnumDeviceInfo(deviceInfo, i, ref deviceData); ++i)
            {
                IntPtr deviceId = Marshal.AllocHGlobal(MAX_DEVICE_ID_LEN);
                if (CM_Get_Device_ID(deviceData.devInst, deviceId, MAX_DEVICE_ID_LEN, 0) != 0)
                {
                    continue;
                }

                if (!IsMatchingDevice(deviceId, uiVID, uiPID, uiMI))
                    continue;

                SP_DEVICE_INTERFACE_DATA interfaceData = new SP_DEVICE_INTERFACE_DATA();
                interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

                if (!SetupDiEnumDeviceInterfaces(deviceInfo, ref deviceData, ref GUID_DEVINTERFACE_HID, 0, ref interfaceData))
                {
                    break;
                }

                uint requiredSize = 0;
                SetupDiGetDeviceInterfaceDetail(deviceInfo, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);

                SP_DEVICE_INTERFACE_DETAIL_DATA interfaceDetailData = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                if (IntPtr.Size == 8) // for 64 bit operating systems
                {
                    interfaceDetailData.cbSize = 8;
                }
                else
                {
                    interfaceDetailData.cbSize = 4 + (uint)Marshal.SystemDefaultCharSize; // for 32 bit systems
                }

                if (!SetupDiGetDeviceInterfaceDetail(deviceInfo, ref interfaceData, ref interfaceDetailData, requiredSize, IntPtr.Zero, IntPtr.Zero))
                {
                    break;
                }

                var deviceHandle = CreateFile(interfaceDetailData.DevicePath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (deviceHandle.ToInt64() == INVALID_HANDLE_VALUE)
                {
                    break;
                }

                returnPointer = deviceHandle;
                byte usb_pkt = new byte();
                usb_pkt = 0;
                bool res = HidD_SetFeature(returnPointer, ref usb_pkt, 65);
                break;
            }

            SetupDiDestroyDeviceInfoList(deviceInfo);
            return returnPointer;
        }

        /// <summary>
        /// C Code by http://www.reddit.com/user/chrisgzy
        /// Converted to C# by http://www.reddit.com/user/billism
        /// </summary>
        private bool IsMatchingDevice(IntPtr pDeviceID, uint uiVID, uint uiPID, uint uiMI)
        {
            var deviceString = Marshal.PtrToStringAuto(pDeviceID);
            if (deviceString == null)
            {
                return false;
            }

            bool isMatch = deviceString.Contains(string.Format("VID_{0:X4}", uiVID));
            isMatch &= deviceString.Contains(string.Format("PID_{0:X4}", uiPID));
            isMatch &= deviceString.Contains(string.Format("MI_{0:X2}", uiMI));

            return isMatch;
        }

        /// <summary>
        /// Initiates the lookup table used as reference for drawing to the keyboard.
        /// </summary>
        /// <returns>True for success, false for failure.</returns>
        private bool InitiateLookupTable()
        {
            if (!LoadSizePositionMaps()) { return false; };

            var keys = KeyboardMap.Positions.GetEnumerator();
            keys.MoveNext();
            var sizes = KeyboardMap.Sizes.GetEnumerator();
            sizes.MoveNext();

            for (int y = 0; y < 7; y++)
            {
                byte key = 0x00;
                int size = 0;

                for (int x = 0; x < KeyboardMap.CanvasWidth; x++)
                {
                    if (size == 0)
                    {
                        try
                        {
                            float sizef = (float)sizes.Current;
                            sizes.MoveNext();
                            if (sizef < 0)
                            {
                                size = (int)(-sizef * 4);
                                key = 255;
                            }
                            else
                            {
                                key = (byte)keys.Current;
                                keys.MoveNext();
                                size = (int)(sizef * 4);
                            }
                        }
                        catch
                        {
                            UpdateStatusMessage.NewMessage(3, "Enumeration Failed.");
                            return false;
                        }
                    }

                    KeyboardMap.LedMatrix[y, x] = key;
                    size--;
                }
                if ((byte)keys.Current != 255 || (float)sizes.Current != 0f)
                {
                    UpdateStatusMessage.NewMessage(4, "Bad line: " + keys.Current + ", " + sizes.Current + " Key " + key + "." + y);
                }

                keys.MoveNext();
                sizes.MoveNext();
            }
             UpdateStatusMessage.NewMessage(4, "Lookup tables initiated.");
            return true;
        }
        
        /// <summary>
        /// Loads the size and position maps for the selected keyboard and layout.
        /// </summary>
        /// <returns>True for success, false for failure.</returns>
        private bool LoadSizePositionMaps()
        {
            // Break if there's no keyboard layout/model selected
            if (Properties.Settings.Default.KeyboardModel == "None" || Properties.Settings.Default.KeyboardLayout == "None")
            {
                UpdateStatusMessage.NewMessage(3, "Invalid keyboard selection.");
                return false;
            };

            // Load data from XML file
            XDocument document = XDocument.Load(System.AppDomain.CurrentDomain.BaseDirectory + "CorsairDevices\\" + Properties.Settings.Default.KeyboardModel + ".xml");
            //XDocument document = XDocument.Parse(GetResourceTextFile("CorsairDevices." + Properties.Settings.Default.KeyboardModel + ".xml"));
            keyboardIDs = document.Descendants("id").Select(element => element.Value).ToArray();
            keyboardNames = document.Descendants("name").Select(element => element.Value).ToArray();
            keyboardPositionMaps = document.Descendants("positionmap").Select(element => element.Value).ToArray();
            keyboardSizeMaps = document.Descendants("sizemap").Select(element => element.Value).ToArray();

            int keyboardIndex = Array.FindIndex(keyboardNames, s => s.Equals(Properties.Settings.Default.KeyboardLayout));

            // Load position and size maps
            string positionMaps = keyboardPositionMaps[keyboardIndex];
            string sizeMaps = keyboardSizeMaps[keyboardIndex];

            // Replace the '.' decimals by whatever the system decimal separator may be, if it's not a period
            char DecimalSep = Convert.ToChar(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            if (DecimalSep != '.')
            {
                positionMaps = positionMaps.Replace('.', DecimalSep);
                sizeMaps = sizeMaps.Replace('.', DecimalSep);
            }

            // Verify the loaded maps
            if (positionMaps.Length > 0 &&
                sizeMaps.Length > 0)
            {
                bool MapFail = false;
                try
                {
                    KeyboardMap.Positions = Array.ConvertAll(positionMaps.Split(';'), byte.Parse);
                }
                catch
                {
                    UpdateStatusMessage.NewMessage(3, "Position Map loading failed.");
                    MapFail = true;
                }

                try
                {
                    KeyboardMap.Sizes = Array.ConvertAll(sizeMaps.Split(';'), float.Parse);
                }
                catch
                {
                    UpdateStatusMessage.NewMessage(3, "Size Map loading failed.");
                    MapFail = true;
                }
                if (MapFail == true) { return false; };
            }
            else
            {
                UpdateStatusMessage.NewMessage(3, "The selected layout is empty");
                return false;
            }
            
            switch (Properties.Settings.Default.KeyboardModel)
            {
                case "K65-RGB":
                    KeyboardMap.CanvasWidth = 76;
                    break;
                case "K70-RGB":
                case "STRAFE":
                    KeyboardMap.CanvasWidth = 92;
                    break;
                case "K95-RGB":
                    KeyboardMap.CanvasWidth = 104;
                    break;
            }
            
            return true;
        }

        public string GetResourceTextFile(string filename)
        {
            string result = string.Empty;

            using (Stream stream = this.GetType().Assembly.
                       GetManifestResourceStream("Corsair_Effects_Engine." + filename))
            {
                using (StreamReader sr = new StreamReader(stream))
                {
                    result = sr.ReadToEnd();
                }
            }
            return result;
        }

        public string FirstLetterToUpper(string str)
        {
            if (str == null)
                return null;

            if (str.Length > 1)
                return char.ToUpper(str[0]) + str.Substring(1);

            return str.ToUpper();
        }
    }
}