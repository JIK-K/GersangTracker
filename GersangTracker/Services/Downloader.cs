using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace GersangTracker.Services
{
    public class DownloadProgress
    {
        public long BytesReceived { get; }
        public long? TotalBytes { get; }
        public double Percentage => TotalBytes > 0 ? (double)BytesReceived / TotalBytes.Value * 100 : 0;

        public DownloadProgress(long bytesReceived, long? totalBytes)
        {
            BytesReceived = bytesReceived;
            TotalBytes = totalBytes;
        }
    }

    public class Downloader
    {
        private static readonly HttpClient _http = new HttpClient();

        public async Task DownloadFileAsync(string url, string destinationPath, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            string tempPath = destinationPath + ".gsdownload";
            long existingLength = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0;

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (existingLength > 0)
                request.Headers.Range = new RangeHeaderValue(existingLength, null); // 이어받기 요청

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            // 서버가 이어받기를 지원하지 않거나 파일이 변경된 경우 처음부터 다시 받음
            if (existingLength > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                existingLength = 0;
                File.Delete(tempPath);
                response.EnsureSuccessStatusCode();
            }

            long? totalBytes = response.Content.Headers.ContentLength;
            if (totalBytes.HasValue) totalBytes += existingLength;

            using var fileStream = new FileStream(tempPath, FileMode.Append, FileAccess.Write, FileShare.None, 8192, true);
            using var downloadStream = await response.Content.ReadAsStreamAsync(ct);

            var buffer = new byte[8192];
            long totalRead = existingLength;
            int read;

            while ((read = await downloadStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, ct);
                totalRead += read;
                progress?.Report(new DownloadProgress(totalRead, totalBytes));
            }

            fileStream.Close();
            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(tempPath, destinationPath);
        }
    }
}