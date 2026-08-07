using GersangTracker.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GersangTracker.Services
{
    public class GameInstallManager
    {
        private readonly Downloader _downloader = new Downloader();
        private readonly SevenZipExtractor _extractor = new SevenZipExtractor();

        public async Task RunAsync(GameServer targetServer, string installPath, IProgress<DownloadProgress> downloadProgress, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(installPath))
                throw new ArgumentException("설치 경로가 올바르지 않습니다.");

            Directory.CreateDirectory(installPath);
            string archivePath = Path.Combine(installPath, "Gersang_Install.7z");
            string archiveUrl = GameServerHelper.GetFullClientUrl(targetServer);

            // 1. 전체 클라이언트 압축 파일 다운로드 (이어받기 지원)
            await _downloader.DownloadFileAsync(archiveUrl, archivePath, downloadProgress, ct);

            // 2. 7-Zip 압축 해제 (엄청난 속도)
            await _extractor.ExtractAsync(archivePath, installPath, ct);

            // 3. 압축이 끝난 설치 파일(8GB) 삭제로 용량 확보
            if (File.Exists(archivePath))
                File.Delete(archivePath);
        }
    }
}