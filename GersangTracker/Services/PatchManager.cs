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
                string vsnPath = Path.Combine(installPath, "Online", "vsn.dat");
                if (!File.Exists(vsnPath)) return null;

                byte[] bytes = File.ReadAllBytes(vsnPath);
                if (bytes.Length >= 4)
                {
                    int raw = BitConverter.ToInt32(bytes, 0);
                    return -(raw + 1); // 거상 방식의 버전 해독
                }
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

                // info 파일을 줄 단위로 분리
                var lines = infoText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    // 주석으로 시작하는 줄은 패스
                    if (line.StartsWith(";") || line.StartsWith("#")) continue;

                    // TSV 형식에 맞춰 탭(\t)으로 컬럼 분리
                    var cols = line.Split('\t');
                    
                    // 인덱스가 초과되는 에러 방지를 위해 최소한 RelativeDir이 있는 4번째 컬럼까지 존재하는지 확인
                    if (cols.Length < 4) continue;

                    string zipFileName = cols[1];
                    string relativeDir = cols[3];

                    // .gsz 파일이 아니면 무시
                    if (!zipFileName.EndsWith(".gsz", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // 백슬래시(\)를 슬래시(/)로 정규화하고 경로 앞뒤의 불필요한 슬래시 제거
                    string relativeDirNormalized = relativeDir.Replace('\\', '/').Trim('/');
                    
                    // URL에 추가될 경로 조합
                    string relativeArchiveUrlPath = string.IsNullOrEmpty(relativeDirNormalized) 
                        ? zipFileName 
                        : $"{relativeDirNormalized}/{zipFileName}";

                    // 2. 패치 압축 파일 다운로드 (.gsz)
                    string fileUrl = GameServerHelper.GetPatchFileUrl(server, relativeArchiveUrlPath);
                    string safeFileName = Path.GetFileName(zipFileName);
                    string localZipPath = Path.Combine(tempDir, safeFileName);

                    await _downloader.DownloadFileAsync(fileUrl, localZipPath, downloadProgress, ct);

                    // 3. 거상 원본 폴더의 "올바른 하위 경로"에 압축 해제 덮어쓰기
                    string extractDestPath = string.IsNullOrEmpty(relativeDirNormalized)
                        ? installPath
                        : Path.Combine(installPath, relativeDirNormalized.Replace('/', Path.DirectorySeparatorChar));

                    await _extractor.ExtractAsync(localZipPath, extractDestPath, extractProgress, ct);
                }

                // 4. 해당 버전 파일 모두 적용 완료 시 버전 숫자 올리기
                string vsnPath = Path.Combine(installPath, "Online", "vsn.dat");
                Directory.CreateDirectory(Path.GetDirectoryName(vsnPath));
                int raw = -(ver + 1);
                File.WriteAllBytes(vsnPath, BitConverter.GetBytes(raw));
            }

            // 패치가 최종 완료되면 임시 폴더 삭제
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}