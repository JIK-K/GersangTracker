using GersangTracker.Models;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GersangTracker.Services
{
    public class PatchManager
    {
        private readonly Downloader _downloader = new Downloader();
        private readonly ZipFileExtractor _extractor = new ZipFileExtractor();
        private readonly HttpClient _http = new HttpClient();

        public int? GetCurrentClientVersion(string installPath)
        {
            try
            {
                string vsnPath = Path.Combine(installPath, "Online", "Gersang.vsn");
                if (!File.Exists(vsnPath)) return null;

                string content = File.ReadAllText(vsnPath);
                if (int.TryParse(content.Trim(), out int version)) return version;
            }
            catch { }
            return null;
        }

        public async Task ApplyPatchAsync(GameServer server, int currentVersion, int targetVersion, string installPath, IProgress<DownloadProgress> downloadProgress, IProgress<ExtractionProgress> extractProgress, CancellationToken ct)
        {
            string tempDir = Path.Combine(installPath, "PatchTemp");
            Directory.CreateDirectory(tempDir);

            for (int ver = currentVersion + 1; ver <= targetVersion; ver++)
            {
                ct.ThrowIfCancellationRequested();

                // 1. 패치 파일 목록(info file) 다운로드
                string infoUrl = GameServerHelper.GetVersionInfoUrl(server, ver);
                byte[] infoBytes = await _http.GetByteArrayAsync(infoUrl, ct);
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                string infoText = Encoding.GetEncoding(949).GetString(infoBytes);

                var patchFiles = infoText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(x => x.EndsWith(".gsz", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var patchFile in patchFiles)
                {
                    // 서버 주소가 절대 경로 슬래시(/)로 오므로 안전하게 파일명만 추출해서 템프에 저장
                    string fileUrl = GameServerHelper.GetPatchFileUrl(server, patchFile.TrimStart('/'));
                    string safeFileName = Path.GetFileName(patchFile);
                    string localZipPath = Path.Combine(tempDir, safeFileName);

                    // 2. 패치 압축 파일 다운로드 (.gsz)
                    await _downloader.DownloadFileAsync(fileUrl, localZipPath, downloadProgress, ct);

                    // 3. 거상 원본 폴더에 그대로 압축 해제 덮어쓰기
                    await _extractor.ExtractAsync(localZipPath, installPath, extractProgress, ct);
                }

                // 4. 해당 버전 파일 모두 적용 완료 시 버전 숫자 올리기
                string vsnPath = Path.Combine(installPath, "Online", "Gersang.vsn");
                Directory.CreateDirectory(Path.GetDirectoryName(vsnPath));
                File.WriteAllText(vsnPath, ver.ToString());
            }

            // 패치가 최종 완료되면 임시 폴더 삭제
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}