using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Telechron.Sdk.Containers;

namespace Telechron.Agent.Containers;

// R-SYS8: clears GPU memory between tenants via `nvidia-smi --gpu-reset`.
// This requires exclusive (no other process using the device) access to
// succeed, which single-tenant scheduling (GpuTenancyPolicy) guarantees --
// the reset running at all is itself a check that no other workload is
// co-resident on the device.
public sealed class NvidiaSmiGpuStateSanitizer(ILogger<NvidiaSmiGpuStateSanitizer> logger) : IGpuStateSanitizer
{
    public async Task SanitizeAsync(IReadOnlyList<string> gpuDeviceIds, CancellationToken ct = default)
    {
        foreach (var deviceId in gpuDeviceIds)
        {
            var startInfo = new ProcessStartInfo("nvidia-smi", $"--gpu-reset -i {deviceId}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start nvidia-smi to sanitize GPU {deviceId}.");
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(ct);
                // R-SYS8 has no fallback: a device that failed to reset must
                // not be handed to the next tenant with the prior tenant's
                // memory still resident.
                throw new InvalidOperationException($"nvidia-smi --gpu-reset failed for GPU {deviceId}: {stderr}");
            }

            logger.LogInformation("GPU {DeviceId} sanitized between tenants.", deviceId);
        }
    }
}
