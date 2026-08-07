using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GersangTracker.Services
{
    public class SevenZipExtractor
    {
        public async Task ExtractAsync(string archivePath, string destinationDirectory, CancellationToken ct)
        {
            // 아까 1단계에서 복사해 둔 7za.exe를 가져와서 씁니다.
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "7za.exe");
            if (!File.Exists(exePath))
                throw new FileNotFoundException("Assets 폴더 안에 7za.exe 파일이 없습니다!", exePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"x \"{archivePath}\" -o\"{destinationDirectory}\" -y",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
                throw new Exception($"7-Zip 압축 해제 실패! (오류 코드: {process.ExitCode})");
        }
    }
}