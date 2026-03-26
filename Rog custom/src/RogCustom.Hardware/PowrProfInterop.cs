using System.Runtime.InteropServices;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace RogCustom.Hardware;

/// <summary>
/// Raw P/Invoke for PowrProf and kernel32. No logging; return error codes or throw.
/// Caller (PowerPlanService) is responsible for logging and handling errors.
/// </summary>
internal static partial class PowrProfInterop
{
    public const uint ERROR_SUCCESS = 0;
    public const uint ERROR_NO_MORE_ITEMS = 259;
    public const uint ERROR_MORE_DATA = 234;

    /// <summary>Enumerate power schemes.</summary>
    public const uint ACCESS_SCHEME = 16;

    [LibraryImport("kernel32.dll", SetLastError = false)]
    public static partial IntPtr LocalFree(IntPtr hMem);

    [LibraryImport("powrprof.dll", SetLastError = false)]
    public static partial uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [LibraryImport("powrprof.dll", SetLastError = false)]
    public static partial uint PowerSetActiveScheme(IntPtr userRootPowerKey, in Guid schemeGuid);

    [LibraryImport("powrprof.dll", SetLastError = false)]
    public static partial uint PowerEnumerate(
        IntPtr rootPowerKey,
        IntPtr schemeGuid,
        IntPtr subGroupOfPowerSettingGuid,
        uint accessFlags,
        uint index,
        byte[] buffer,
        ref uint bufferSize);

    [LibraryImport("powrprof.dll", SetLastError = false)]
    public static partial uint PowerReadFriendlyName(
        IntPtr rootPowerKey,
        in Guid schemeGuid,
        IntPtr subGroupOfPowerSettingsGuid,
        IntPtr powerSettingGuid,
        byte[]? buffer,
        ref uint bufferSize);

    /// <summary>
    /// Gets the active power scheme GUID. Caller MUST call LocalFree on the returned pointer on success.
    /// Returns (null, errorCode) on failure; (ptr, 0) on success - caller frees ptr.
    /// </summary>
    public static (IntPtr? GuidPtr, uint ErrorCode) GetActiveSchemeGuid()
    {
        uint err = PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr);
        if (err != ERROR_SUCCESS)
            return (null, err);
        return (ptr, 0);
    }

    /// <summary>
    /// Enumerates power scheme GUIDs. Before each PowerEnumerate call, bufferSize is set to sizeof(Guid).
    /// Stops when ERROR_NO_MORE_ITEMS (259) is returned. Other errors are returned for caller to handle.
    /// </summary>
    public static (List<Guid>? Schemes, uint ErrorCode) EnumerateSchemeGuids()
    {
        var list = new List<Guid>();
        uint index = 0;
        var buffer = new byte[16]; // sizeof(Guid)
        uint bufferSize = (uint)buffer.Length;

        while (true)
        {
            bufferSize = (uint)buffer.Length;
            uint err = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ACCESS_SCHEME, index, buffer, ref bufferSize);
            if (err == ERROR_NO_MORE_ITEMS)
                return (list, ERROR_SUCCESS);
            if (err != ERROR_SUCCESS)
                return (null, err);
            if (bufferSize >= 16)
                list.Add(new Guid(buffer.AsSpan(0, 16)));
            index++;
        }
    }

    /// <summary>
    /// Reads the friendly name for a power scheme. Query-by-GUID; uses buffer-size / ERROR_MORE_DATA retry pattern.
    /// First call with null buffer to get required size; allocate and retry until success.
    /// </summary>
    public static (string? FriendlyName, uint ErrorCode) ReadFriendlyName(Guid schemeGuid)
    {
        uint bufferSize = 0;
        uint err = PowerReadFriendlyName(IntPtr.Zero, schemeGuid, IntPtr.Zero, IntPtr.Zero, null, ref bufferSize);
        if (err != ERROR_SUCCESS)
            return (null, err);
        if (bufferSize == 0)
            return (string.Empty, ERROR_SUCCESS);

        while (true)
        {
            var buffer = new byte[bufferSize];
            uint size = bufferSize;
            err = PowerReadFriendlyName(IntPtr.Zero, schemeGuid, IntPtr.Zero, IntPtr.Zero, buffer, ref size);
            if (err == ERROR_SUCCESS)
            {
                var str = System.Text.Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(buffer.Length, size));
                return (str.TrimEnd('\0'), ERROR_SUCCESS);
            }
            if (err == ERROR_MORE_DATA && size > bufferSize)
            {
                bufferSize = size;
                continue;
            }
            return (null, err);
        }
    }
}
