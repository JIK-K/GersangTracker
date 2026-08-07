using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GersangTracker.ViewModels
{
    public partial class PatchViewModel : ObservableObject
    {
        private readonly PatchManager _patchManager = new PatchManager();
        private readonly GameInstallManager _installManager = new GameInstallManager();
        private CancellationTokenSource _cts;

        [ObservableProperty]
        private string _statusText = "준비 중...";

        [ObservableProperty]
        private string _detailText = "";

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private bool _isPatching;

        public Action CloseAction { get; set; }

        public void StartPatch(GameServer server, int? currentVersion, int targetVersion, string installPath)
        {
            _cts = new CancellationTokenSource();
            IsPatching = true;
            ProgressValue = 0;

            Task.Run(async () =>
            {
                try
                {
                    var downloadProgress = new Progress<DownloadProgress>(p =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusText = "다운로드 중...";
                            DetailText = p.TotalBytes.HasValue
                                ? $"{(p.BytesReceived / 1024.0 / 1024.0):F2} MB / {(p.TotalBytes.Value / 1024.0 / 1024.0):F2} MB ({p.Percentage:F1}%)"
                                : $"{(p.BytesReceived / 1024.0 / 1024.0):F2} MB 다운로드됨";
                            ProgressValue = p.Percentage;
                        });
                    });

                    var extractProgress = new Progress<ExtractionProgress>(p =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusText = "압축 해제 중...";
                            DetailText = $"현재 파일: {p.CurrentFile} ({p.ProcessedEntries}/{p.TotalEntries})";
                            ProgressValue = p.Percentage;
                        });
                    });

                    if (currentVersion == null)
                    {
                        Application.Current.Dispatcher.Invoke(() => StatusText = "전체 클라이언트 설치 준비 중...");
                        await _installManager.RunAsync(server, installPath, downloadProgress, _cts.Token);
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() => StatusText = "업데이트 패치 준비 중...");
                        await _patchManager.ApplyPatchAsync(server, currentVersion.Value, targetVersion, installPath, downloadProgress, extractProgress, _cts.Token);
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = "완료되었습니다!";
                        DetailText = "게임 실행 준비 완료";
                        ProgressValue = 100;
                        MessageBox.Show("패치 작업이 성공적으로 완료되었습니다.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                        CloseAction?.Invoke();
                    });
                }
                catch (OperationCanceledException)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = "취소됨";
                        DetailText = "사용자에 의해 취소되었습니다.";
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = "오류 발생";
                        DetailText = ex.Message;
                        MessageBox.Show($"작업 중 오류가 발생했습니다.\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    IsPatching = false;
                }
            });
        }

        [RelayCommand]
        private void Cancel()
        {
            if (IsPatching)
            {
                var result = MessageBox.Show("진행 중인 작업을 취소하시겠습니까?", "확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _cts?.Cancel();
                }
            }
            else
            {
                CloseAction?.Invoke();
            }
        }
    }
}