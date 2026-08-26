using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace EmailSummarizer.Services
{
    public class LlamaServerManager : IDisposable
    {
        private Process? _process;
        private bool _weStartedServer;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        private IntPtr _jobHandle = IntPtr.Zero;

        public bool IsRunning => _process != null && !_process.HasExited;

        public LlamaServerManager()
        {
            InitializeJobObject();
            
            // Ensure cleanup on any exit scenario
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Stop();
        }

        #region Windows Job Object (Guarantees OS-level kill on parent exit)

        private void InitializeJobObject()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    _jobHandle = CreateJobObject(IntPtr.Zero, null!);
                    var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    };
                    var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                    {
                        BasicLimitInformation = info
                    };

                    int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                    IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                    try
                    {
                        Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
                        SetInformationJobObject(_jobHandle, 9, extendedInfoPtr, (uint)length);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(extendedInfoPtr);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[JobObject] Failed to initialize: {ex.Message}");
                }
            }
        }

        private void AssignToJob(Process process)
        {
            if (_jobHandle != IntPtr.Zero && !process.HasExited)
            {
                try
                {
                    AssignProcessToJobObject(_jobHandle, process.Handle);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[JobObject] Failed to assign process: {ex.Message}");
                }
            }
        }

        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryLimit;
            public UIntPtr PeakJobMemoryLimit;
        }

        #endregion

        public static string ResolveLlamaServerExecutable()
        {
            string[] directCandidates = new[]
            {
                "llama-server.exe",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "llama-server.exe")
            };

            foreach (var path in directCandidates)
            {
                if (File.Exists(path)) return Path.GetFullPath(path);
            }

            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string winGetDir = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
                if (Directory.Exists(winGetDir))
                {
                    var matches = Directory.GetFiles(winGetDir, "llama-server.exe", SearchOption.AllDirectories);
                    if (matches.Length > 0)
                    {
                        return matches[0];
                    }
                }
            }
            catch { }

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var folder in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(folder.Trim(), "llama-server.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch { }
            }

            return "llama-server.exe";
        }

        public async Task<bool> IsServerReadyAsync(string host = "127.0.0.1", int port = 8080, CancellationToken ct = default)
        {
            try
            {
                var response = await HttpClient.GetAsync($"http://{host}:{port}/health", ct);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> StartAsync(
            string modelPath,
            int port = 8080,
            int ngl = 99,
            string host = "127.0.0.1",
            int waitTimeoutSeconds = 35,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            // Thread-safe mutex lock: Prevent duplicate concurrent server launches
            await _lock.WaitAsync(ct);
            try
            {
                // 1. If our process is already alive and running, simply wait for /health or return
                if (_process != null && !_process.HasExited)
                {
                    if (await IsServerReadyAsync(host, port, ct))
                    {
                        return true;
                    }
                }

                // 2. Check if an active server is already listening on the port
                if (await IsServerReadyAsync(host, port, ct))
                {
                    logger?.Report($"[✓] llama-server is active on http://{host}:{port}");
                    return true;
                }

                if (!File.Exists(modelPath))
                {
                    logger?.Report($"[!] Model file not found at: {modelPath}");
                    return false;
                }

                string exePath = ResolveLlamaServerExecutable();
                string modelName = Path.GetFileName(modelPath);
                logger?.Report($"[*] Launching '{Path.GetFileName(exePath)}' with '{modelName}' on port {port} (GPU offload: {ngl} layers)...");

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"-m \"{modelPath}\" --port {port} --host {host} -ngl {ngl} -c 4096 -lv 0",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                _process = Process.Start(psi);
                if (_process == null)
                {
                    logger?.Report("[!] Failed to spawn llama-server process.");
                    return false;
                }

                _weStartedServer = true;

                // Bind child process to Windows Job Object for guaranteed cleanup on parent exit
                AssignToJob(_process);

                // Wait for server to become responsive on /health
                var startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime).TotalSeconds < waitTimeoutSeconds)
                {
                    if (ct.IsCancellationRequested)
                    {
                        StopInternal(logger);
                        return false;
                    }

                    if (_process.HasExited)
                    {
                        logger?.Report($"[!] llama-server exited early with code {_process.ExitCode}");
                        _process = null;
                        _weStartedServer = false;
                        return false;
                    }

                    if (await IsServerReadyAsync(host, port, ct))
                    {
                        logger?.Report($"[✓] llama-server is ready and listening on http://{host}:{port}");
                        return true;
                    }

                    await Task.Delay(400, ct);
                }

                logger?.Report("[!] Timed out waiting for llama-server to initialize.");
                StopInternal(logger);
                return false;
            }
            catch (Exception ex)
            {
                logger?.Report($"[!] Failed to launch llama-server: {ex.Message}");
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Stop(IProgress<string>? logger = null)
        {
            _lock.Wait();
            try
            {
                StopInternal(logger);
            }
            finally
            {
                _lock.Release();
            }
        }

        private void StopInternal(IProgress<string>? logger = null)
        {
            if (_weStartedServer && _process != null)
            {
                logger?.Report("[*] Freeing GPU VRAM: Stopping llama-server...");
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(true);
                        _process.WaitForExit(3000);
                    }
                    logger?.Report("[✓] llama-server stopped. GPU VRAM released successfully.");
                }
                catch (Exception ex)
                {
                    logger?.Report($"[!] Error stopping llama-server: {ex.Message}");
                }
                finally
                {
                    _process?.Dispose();
                    _process = null;
                    _weStartedServer = false;
                }
            }
        }

        public void Dispose()
        {
            Stop();
            if (_jobHandle != IntPtr.Zero)
            {
                CloseHandle(_jobHandle);
                _jobHandle = IntPtr.Zero;
            }
            _lock.Dispose();
        }
    }
}
