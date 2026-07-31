using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace YaraInstaller;

internal static class Program
{
    private const string AppName = "yara";
    private const string InstallerExe = "yara-uninstaller.exe";
    private const string AppExe = "yara.exe";
    private const string IconFile = "yara.ico";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\yara";

    private static readonly string[] RuntimeDlls =
    {
        "libfftw3.dll",
        "glew32.dll",
        "SDL2.dll",
        "libwinpthread-1.dll",
    };

    private static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);

    private static string AppExePath => Path.Combine(InstallDir, AppExe);
    private static string IconPath => Path.Combine(InstallDir, IconFile);
    private static string UninstallerPath => Path.Combine(InstallDir, InstallerExe);
    private static string StartMenuPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "yara.lnk");

    public static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
                return Uninstall();
            return Install(silent: args.Contains("--silent", StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("yara installer: " + ex.Message);
            return 1;
        }
    }

    private static int Install(bool silent)
    {
        Console.WriteLine("yara installer");
        Console.WriteLine("==============");

        Directory.CreateDirectory(InstallDir);
        WriteResource("yara.exe.gz", AppExePath, decompress: true);
        foreach (string dll in RuntimeDlls)
            WriteResource(dll + ".gz", Path.Combine(InstallDir, dll), decompress: true);
        WriteResource("yara.ico", IconPath, decompress: false);
        CopySelfTo(UninstallerPath);

        CreateStartMenuShortcut();
        AddToPath(InstallDir);
        WriteUninstallRegistry();

        Console.WriteLine();
        Console.WriteLine("yara installed to:");
        Console.WriteLine("  " + InstallDir);
        Console.WriteLine("Added to your user PATH (new terminal windows will pick it up).");
        Console.WriteLine("Run it with the command:  yara");
        Console.WriteLine("Config file: " + Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "yara", "config"));
        Console.WriteLine();
        if (!silent && !Console.IsInputRedirected)
        {
            Console.Write("Press Enter to launch yara now, or any other key to finish... ");
            var key = Console.ReadKey(true);
            Console.WriteLine();
            if (key.Key == ConsoleKey.Enter)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c start \"\" \"yara\"",
                    UseShellExecute = true
                });
            }
        }
        return 0;
    }

    private static int Uninstall()
    {
        Console.WriteLine("yara uninstaller");
        Console.WriteLine("================");

        if (File.Exists(StartMenuPath))
        {
            File.Delete(StartMenuPath);
            Console.WriteLine("Removed Start Menu shortcut.");
        }
        RemoveFromPath(InstallDir);
        RemoveUninstallRegistry();

        try
        {
            if (Directory.Exists(InstallDir))
            {
                Directory.Delete(InstallDir, recursive: true);
                Console.WriteLine("Removed " + InstallDir);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Note: could not delete files (they may be in use): " + ex.Message);
        }
        Console.WriteLine("yara has been uninstalled.");

        string? self = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(self) &&
            string.Equals(Path.GetFullPath(self), Path.GetFullPath(UninstallerPath), StringComparison.OrdinalIgnoreCase))
        {
            ScheduleSelfDelete();
        }
        return 0;
    }

    private static void ScheduleSelfDelete()
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = "/c timeout /t 2 /nobreak >nul & del /q \"" + UninstallerPath + "\" & rmdir /s /q \"" + InstallDir + "\" 2>nul"
            };
            Process.Start(psi);
        }
        catch
        {
        }
    }

    private static void WriteResource(string name, string dest, bool decompress)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var input = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Embedded resource not found: " + name);
        using var output = File.Create(dest);
        if (decompress)
        {
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            gz.CopyTo(output);
        }
        else
        {
            input.CopyTo(output);
        }
        Console.WriteLine("Wrote " + dest);
    }

    private static void CopySelfTo(string dest)
    {
        string? self = Environment.ProcessPath;
        if (string.IsNullOrEmpty(self) || !File.Exists(self)) return;
        if (string.Equals(Path.GetFullPath(self), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) return;
        File.Copy(self, dest, overwrite: true);
    }

    private static void CreateStartMenuShortcut()
    {
        try
        {
            Type? t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return;
            dynamic shell = Activator.CreateInstance(t)!;
            dynamic link = shell.CreateShortcut(StartMenuPath);
            link.TargetPath = AppExePath;
            link.WorkingDirectory = InstallDir;
            link.IconLocation = IconPath + ",0";
            link.Description = "yara - realtime audio visualizer";
            link.Save();
            Console.WriteLine("Created Start Menu shortcut.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Note: could not create Start Menu shortcut: " + ex.Message);
        }
    }

    private static void WriteUninstallRegistry()
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath);
        if (key == null) return;
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        key.SetValue("DisplayName", "yara - realtime audio visualizer");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "yara contributors");
        key.SetValue("DisplayIcon", AppExePath);
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("UninstallString", "\"" + UninstallerPath + "\" --uninstall");
        key.SetValue("QuietUninstallString", "\"" + UninstallerPath + "\" --uninstall --silent");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", 6000, RegistryValueKind.DWord);
        Console.WriteLine("Registered in Apps & Features.");
    }

    private static void RemoveUninstallRegistry()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
            Console.WriteLine("Removed Apps & Features entry.");
        }
        catch
        {
        }
    }

    private static void AddToPath(string dir)
    {
        if (!TryGetUserPath(out string? current, out RegistryKey? env) || env == null)
        {
            Console.WriteLine("Note: could not update PATH.");
            return;
        }
        using (env)
        {
            var entries = (current ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            if (entries.Any(e => e.Equals(dir, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("PATH already contains " + dir);
                return;
            }
            entries.Add(dir);
            env.SetValue("Path", string.Join(';', entries), RegistryValueKind.ExpandString);
        }
        Console.WriteLine("Added to PATH.");
        BroadcastPathChanged();
    }

    private static void RemoveFromPath(string dir)
    {
        if (!TryGetUserPath(out string? current, out RegistryKey? env) || env == null)
        {
            Console.WriteLine("Note: could not update PATH.");
            return;
        }
        using (env)
        {
            var entries = (current ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Where(e => !e.Equals(dir, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (entries.Count == 0)
            {
                env.DeleteValue("Path", throwOnMissingValue: false);
            }
            else
            {
                env.SetValue("Path", string.Join(';', entries), RegistryValueKind.ExpandString);
            }
        }
        BroadcastPathChanged();
    }

    private static bool TryGetUserPath(out string? current, out RegistryKey? env)
    {
        current = null;
        env = Registry.CurrentUser.OpenSubKey("Environment", writable: true);
        if (env == null) return false;
        current = (string?)env.GetValue("Path", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return true;
    }

    private static void BroadcastPathChanged()
    {
        SendMessageTimeout(
            (IntPtr)0xFFFF, 0x001A, IntPtr.Zero, "Environment", 0x0002, 3000, out _);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);
}
