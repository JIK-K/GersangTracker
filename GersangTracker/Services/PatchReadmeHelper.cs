using GersangTracker.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GersangTracker.Services
{
    public static class PatchReadmeHelper
    {
        private static readonly Regex BlockRegex = new Regex(
            @"-(?<date>\d{4}\.\d{2}\.\d{2})-\s*\r?\n\[거상 패치 V(?<version>\d+)\]\s*\r?\n(?<body>.*?)(?=(?:\r?\n-\d{4}\.\d{2}\.\d{2}-)|\z)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public static async Task<List<PatchReadmeInfoItem>> GetPatchInfoListAsync(GameServer server, CancellationToken ct = default)
        {
            string readmeUrl = GameServerHelper.GetReadMeUrl(server);
            string readmeText = await DownloadReadMeAsync(readmeUrl, ct);
            return Parse(readmeUrl, readmeText);
        }

        public static async Task<int?> GetLatestVersionAsync(GameServer server, CancellationToken ct = default)
        {
            var list = await GetPatchInfoListAsync(server, ct);
            return list.FirstOrDefault()?.Version;
        }

        private static List<PatchReadmeInfoItem> Parse(string readmeUrl, string readmeText, int count = int.MaxValue)
        {
            if (string.IsNullOrWhiteSpace(readmeText))
                throw new InvalidDataException("Patch readme content was empty.");

            List<PatchReadmeInfoItem> result = new();
            var matches = BlockRegex.Matches(readmeText);

            foreach (Match match in matches.Cast<Match>())
            {
                string dateText = match.Groups["date"].Value;
                string versionText = match.Groups["version"].Value;
                string bodyText = match.Groups["body"].Value;

                if (DateTime.TryParseExact(dateText, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) &&
                    int.TryParse(versionText, out int version))
                {
                    List<string> details = bodyText
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToList();

                    result.Add(new PatchReadmeInfoItem(date, version, details));
                    if (result.Count >= count)
                        break;
                }
            }

            return result;
        }

        private static async Task<string> DownloadReadMeAsync(string readmeUrl, CancellationToken ct = default)
        {
            using var http = new HttpClient();
            byte[] bytes = await http.GetByteArrayAsync(readmeUrl, ct);

            // 거상 서버는 한글 인코딩(euc-kr/949)을 사용합니다.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(949).GetString(bytes);
        }
    }
}