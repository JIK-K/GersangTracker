using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace GersangTracker.Services
{
    public class ExtractionProgress
    {
        public int ProcessedEntries { get; }
        public int TotalEntries { get; }
        public double Percentage => TotalEntries > 0 ? (double)ProcessedEntries / TotalEntries * 100 : 0;
        public string CurrentFile { get; }

        public ExtractionProgress(int processed, int total, string currentFile)
        {
            ProcessedEntries = processed;
            TotalEntries = total;
            CurrentFile = currentFile;
        }
    }

    public class ZipFileExtractor
    {
        public async Task ExtractAsync(string archivePath, string destinationDirectory, IProgress<ExtractionProgress> progress, CancellationToken ct)
        {
            using var archive = ZipFile.OpenRead(archivePath);
            int total = archive.Entries.Count;
            int processed = 0;

            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(entry.Name)) continue; // 폴더는 스킵

                string destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                using (var entryStream = entry.Open())
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    await entryStream.CopyToAsync(fileStream, 8192, ct);
                }

                processed++;
                progress?.Report(new ExtractionProgress(processed, total, entry.Name));
            }
        }
    }
}