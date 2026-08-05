using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FFGuardian;

internal sealed record AuthenticodeResult100(
    bool IsSigned,
    bool IsTrusted,
    string Signer,
    string Status,
    int NativeStatus);

internal static partial class AuthenticodeVerifier100
{
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    public static AuthenticodeResult100 Verify(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            return new(false, false, string.Empty, "File non trovato", unchecked((int)0x80070002));

        string signer = ReadSigner(filePath);
        bool signed = !string.IsNullOrWhiteSpace(signer);
        int status = VerifyTrust(filePath);

        return status switch
        {
            0 => new(true, true, signer, $"Firma valida: {signer}", status),
            unchecked((int)0x800B0100) => new(false, false, string.Empty, "Firma digitale assente", status),
            unchecked((int)0x80096010) => new(signed, false, signer, "Firma presente ma contenuto modificato", status),
            unchecked((int)0x800B0101) => new(signed, false, signer, "Certificato scaduto o non ancora valido", status),
            unchecked((int)0x800B0109) => new(signed, false, signer, "Catena del certificato non attendibile", status),
            _ => new(signed, false, signer, $"Firma non attendibile (0x{status:X8})", status)
        };
    }

    private static int VerifyTrust(string filePath)
    {
        using WinTrustFileInfo fileInfo = new(filePath);
        using WinTrustData trustData = new(fileInfo);
        Guid action = GenericVerifyV2;
        return WinVerifyTrust(IntPtr.Zero, ref action, trustData.Pointer);
    }

    private static string ReadSigner(string filePath)
    {
        try
        {
#pragma warning disable SYSLIB0026
            using X509Certificate certificate = X509Certificate.CreateFromSignedFile(filePath);
#pragma warning restore SYSLIB0026
            using X509Certificate2 certificate2 = new(certificate);
            return certificate2.GetNameInfo(X509NameType.SimpleName, false);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }

    [LibraryImport("wintrust.dll", EntryPoint = "WinVerifyTrust", SetLastError = true)]
    private static partial int WinVerifyTrust(IntPtr hwnd, ref Guid actionId, IntPtr trustData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfoNative
    {
        public uint StructSize;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustDataNative
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public string? UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    private sealed class WinTrustFileInfo : IDisposable
    {
        private IntPtr _filePath;
        public IntPtr Pointer { get; private set; }

        public WinTrustFileInfo(string filePath)
        {
            _filePath = Marshal.StringToCoTaskMemUni(filePath);
            WinTrustFileInfoNative native = new()
            {
                StructSize = (uint)Marshal.SizeOf<WinTrustFileInfoNative>(),
                FilePath = _filePath,
                FileHandle = IntPtr.Zero,
                KnownSubject = IntPtr.Zero
            };
            Pointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfoNative>());
            Marshal.StructureToPtr(native, Pointer, false);
        }

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(Pointer);
                Pointer = IntPtr.Zero;
            }
            if (_filePath != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(_filePath);
                _filePath = IntPtr.Zero;
            }
        }
    }

    private sealed class WinTrustData : IDisposable
    {
        public IntPtr Pointer { get; private set; }

        public WinTrustData(WinTrustFileInfo fileInfo)
        {
            WinTrustDataNative native = new()
            {
                StructSize = (uint)Marshal.SizeOf<WinTrustDataNative>(),
                PolicyCallbackData = IntPtr.Zero,
                SipClientData = IntPtr.Zero,
                UiChoice = 2,
                RevocationChecks = 0,
                UnionChoice = 1,
                FileInfo = fileInfo.Pointer,
                StateAction = 0,
                StateData = IntPtr.Zero,
                UrlReference = null,
                ProviderFlags = 0x00000010,
                UiContext = 0,
                SignatureSettings = IntPtr.Zero
            };
            Pointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustDataNative>());
            Marshal.StructureToPtr(native, Pointer, false);
        }

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero)
            {
                Marshal.DestroyStructure<WinTrustDataNative>(Pointer);
                Marshal.FreeCoTaskMem(Pointer);
                Pointer = IntPtr.Zero;
            }
        }
    }
}
