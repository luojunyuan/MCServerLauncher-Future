using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MCServerLauncher.Common.Helpers;
using MCServerLauncher.Common.ProtoType.Status;
using Microsoft.Win32;

namespace MCServerLauncher.Daemon.Utils.Status;

public static class CpuInfoHelper
{
    public static readonly string Vendor;
    public static readonly string Name;
    public static readonly int ProcessorCount = Environment.ProcessorCount;
    public static readonly int CoreCount;
    public static readonly int ThreadCount;

    static CpuInfoHelper()
    {
        var (name, vendor, coreCount, threadCount) = GetCpuInfoSnapshot();
        Name = name;
        Vendor = vendor;
        CoreCount = coreCount;
        ThreadCount = threadCount;
    }

    private static (string Name, string Vendor, int CoreCount, int ThreadCount) GetCpuInfoSnapshot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetWindowsCpuInfoSnapshot();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var cpuInfo = File.ReadAllLines("/proc/cpuinfo");
            var name = ReadLinuxCpuField(cpuInfo, "model name") ?? "Unknown";
            var vendor = ReadLinuxCpuField(cpuInfo, "vendor_id") ?? "Unknown";
            var coreCount = GetLinuxPhysicalCoreCount(cpuInfo);
            return (name, vendor, coreCount, ProcessorCount);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var task = SystemInfoHelper.RunCommandAsync("sysctl", "-n machdep.cpu.brand_string");
            task.Wait();
            var name = task.Result.Trim();
            var vendor = name.Split(' ')[0];
            return (name, vendor, GetMacOsProcessorCount("hw.physicalcpu"), GetMacOsProcessorCount("hw.logicalcpu"));
        }

        throw new NotSupportedException("Unsupported OS");
    }

    public static async Task<CpuInfo> GetCpuInfo()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return CreateCpuInfo(await GetWinCpuUsage());

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var cpuUsage =
                await SystemInfoHelper.RunCommandAsync("sh",
                        "-c \"top -l 1 | grep 'CPU usage' | awk '{print $3}' | tr -d '%'\"")
                    .MapTask(ParseMacOsCpuUsage);

            var name = await SystemInfoHelper.RunCommandAsync("sysctl", "-n machdep.cpu.brand_string");
            var vendor = name.Split(' ')[0];
            return new CpuInfo(
                vendor,
                name,
                ThreadCount,
                cpuUsage,
                CoreCount,
                ThreadCount);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var usage = await GetLinuxCpuUsage();
            return CreateCpuInfo(usage);
        }

        throw new NotSupportedException("Unsupported OS");
    }

    internal static double ParseMacOsCpuUsage(string output)
    {
        return double.TryParse(
            output.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var usage)
            ? usage
            : 0;
    }

    private static CpuInfo CreateCpuInfo(double usage)
    {
        return new CpuInfo(Vendor, Name, ThreadCount, usage, CoreCount, ThreadCount);
    }

    [SupportedOSPlatform("windows")]
    private static (string Name, string Vendor, int CoreCount, int ThreadCount) GetWindowsCpuInfoSnapshot()
    {
        var name = "Unknown";
        var vendor = "Unknown";

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            name = ReadRegistryString(key, "ProcessorNameString") ?? name;
            vendor = ReadRegistryString(key, "VendorIdentifier") ?? vendor;
        }
        catch (Exception)
        {
            // Registry access is optional; processor counts still provide useful status data.
        }

        var threadCount = NormalizeProcessorCount(ProcessorCount);
        var coreCount = GetWindowsPhysicalCoreCount(threadCount);
        return (name, vendor, coreCount, threadCount);
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadRegistryString(RegistryKey? key, string valueName)
    {
        return key?.GetValue(valueName)?.ToString()?.Trim() is { Length: > 0 } value ? value : null;
    }

    private static int GetWindowsPhysicalCoreCount(int fallback)
    {
        uint bufferLength = 0;
        GetLogicalProcessorInformationEx(
            LogicalProcessorRelationship.RelationProcessorCore,
            IntPtr.Zero,
            ref bufferLength);

        if (bufferLength == 0) return fallback;

        var buffer = Marshal.AllocHGlobal(checked((int)bufferLength));
        try
        {
            if (!GetLogicalProcessorInformationEx(
                    LogicalProcessorRelationship.RelationProcessorCore,
                    buffer,
                    ref bufferLength))
                return fallback;

            var totalLength = checked((int)bufferLength);
            var offset = 0;
            var coreCount = 0;
            while (offset + sizeof(int) * 2 <= totalLength)
            {
                var recordSize = Marshal.ReadInt32(buffer, offset + sizeof(int));
                if (recordSize < sizeof(int) * 2 || offset + recordSize > totalLength) break;
                coreCount++;
                offset += recordSize;
            }

            return NormalizeProcessorCount(coreCount);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string? ReadLinuxCpuField(string[] cpuInfo, string fieldName)
    {
        var prefix = fieldName + "\t:";
        return cpuInfo.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal))?
            .Split(':', 2)[1]
            .Trim();
    }

    private static int GetLinuxPhysicalCoreCount(string[] cpuInfo)
    {
        var physicalCores = new HashSet<string>(StringComparer.Ordinal);
        var physicalId = string.Empty;
        var coreId = string.Empty;

        foreach (var line in cpuInfo)
        {
            if (line.Length == 0)
            {
                AddLinuxCore(physicalCores, physicalId, coreId);
                physicalId = string.Empty;
                coreId = string.Empty;
                continue;
            }

            if (line.StartsWith("physical id", StringComparison.Ordinal))
            {
                physicalId = line.Split(':', 2)[1].Trim();
            }
            else if (line.StartsWith("core id", StringComparison.Ordinal))
            {
                coreId = line.Split(':', 2)[1].Trim();
            }
        }

        AddLinuxCore(physicalCores, physicalId, coreId);
        return NormalizeProcessorCount(physicalCores.Count);
    }

    private static void AddLinuxCore(HashSet<string> physicalCores, string physicalId, string coreId)
    {
        if (string.IsNullOrWhiteSpace(physicalId) || string.IsNullOrWhiteSpace(coreId)) return;
        physicalCores.Add($"{physicalId}:{coreId}");
    }

    private static int GetMacOsProcessorCount(string key)
    {
        try
        {
            var task = SystemInfoHelper.RunCommandAsync("sysctl", $"-n {key}");
            task.Wait();
            return int.TryParse(task.Result.Trim(), out var count)
                ? NormalizeProcessorCount(count)
                : ProcessorCount;
        }
        catch
        {
            return ProcessorCount;
        }
    }

    private static int NormalizeProcessorCount(int value)
    {
        return value > 0 ? value : Math.Max(1, ProcessorCount);
    }

    private static Task<(ulong, ulong)> GetLinuxCpuTime()
    {
        return SystemInfoHelper.RunCommandAsync(
            "sh",
            "-c \"cat /proc/stat | grep '^cpu ' | awk '{total=0; for(i=2;i<=NF;i++) total+=$i; idle=$5+$6; print total, idle}'\""
        ).MapTask(x =>
        {
            var rv = x.Split(' ').Select(ulong.Parse).ToArray();
            return (rv[0], rv[1]);
        });
    }

    private static async Task<double> GetLinuxCpuUsage(int delay = 300)
    {
        var (total1, idle1) = await GetLinuxCpuTime();

        await Task.Delay(delay);

        var (total2, idle2) = await GetLinuxCpuTime();

        var totalDiff = total2 - total1;
        var idleDiff = idle2 - idle1;

        return totalDiff == 0 ? 0 : (1.0 - (double)idleDiff / totalDiff) * 100;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime,
        out FILETIME lpKernelTime,
        out FILETIME lpUserTime);

    public static async Task<double> GetWinCpuUsage(int samplingInterval = 300)
    {
        // 第一次采样
        var (idleTime1, totalTime1) = GetWinCpuTime();

        // 异步等待采样间隔
        await Task.Delay(samplingInterval);

        // 第二次采样
        var (idleTime2, totalTime2) = GetWinCpuTime();

        // 计算差值
        var idleDelta = idleTime2 - idleTime1;
        var totalDelta = totalTime2 - totalTime1;

        // 处理除零情况
        if (totalDelta == 0) return 0;

        // 计算使用率
        return 100.0 * (1.0 - (double)idleDelta / totalDelta);
    }

    private static (ulong IdleTime, ulong TotalTime) GetWinCpuTime()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(
                error, "获取系统时间失败");
        }

        // 转换时间值为 UInt64
        var idle = ((ulong)idleTime.dwHighDateTime << 32) | idleTime.dwLowDateTime;
        var kernel = ((ulong)kernelTime.dwHighDateTime << 32) | kernelTime.dwLowDateTime;
        var user = ((ulong)userTime.dwHighDateTime << 32) | userTime.dwLowDateTime;

        // 关键修正：内核时间已包含空闲时间，总时间 = 内核时间 + 用户时间
        return (idle, kernel + user);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    private enum LogicalProcessorRelationship
    {
        RelationProcessorCore = 0
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(
        LogicalProcessorRelationship relationship,
        IntPtr buffer,
        ref uint returnedLength);
}
