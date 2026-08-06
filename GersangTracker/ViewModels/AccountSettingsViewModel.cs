using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace GersangTracker.ViewModels
{
    public partial class AccountSettingsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        public Account Account { get; }

        [ObservableProperty] private string _userId;
        [ObservableProperty] private string _password;
        [ObservableProperty] private string _clientPath;

        // 메인 뷰모델에서 삭제 여부를 알 수 있도록 플래그 제공
        public bool IsDeleted { get; private set; }

        public AccountSettingsViewModel(Account account, DatabaseService databaseService)
        {
            Account = account;
            _databaseService = databaseService;

            UserId = account.UserId ?? string.Empty;
            Password = account.EncryptedPassword ?? string.Empty;
            ClientPath = account.ClientInstance?.InstallPath ?? string.Empty;
        }

        [RelayCommand]
        private void BrowseClientPath()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "거상 클라이언트 설치 폴더 선택"
            };

            if (dialog.ShowDialog() == true)
            {
                ClientPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private async Task SaveAsync(Window window)
        {
            if (string.IsNullOrWhiteSpace(UserId))
            {
                MessageBox.Show("아이디를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Account.UserId = UserId;
            Account.EncryptedPassword = Password;

            if (Account.ClientInstance == null)
            {
                Account.ClientInstance = new ClientInstance { AccountId = Account.Id };
            }
            Account.ClientInstance.InstallPath = ClientPath;

            await _databaseService.AddOrUpdateAccountAsync(Account);

            window.DialogResult = true;
            window.Close();
        }

        [RelayCommand]
        private async Task DeleteAccountAsync(Window window)
        {
            // 아직 만들어지지도 않은 계정을 삭제하려 하면 그냥 닫음
            if (Account.Id == 0)
            {
                window.DialogResult = false;
                window.Close();
                return;
            }

            var result = MessageBox.Show($"'{UserId}' 계정을 정말 삭제하시겠습니까?\n이 계정의 모든 몬스터 및 사냥 기록이 완전히 삭제됩니다.",
                                         "계정 삭제 확인",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _databaseService.DeleteAccountAsync(Account.Id);
                IsDeleted = true;
                window.DialogResult = true;
                window.Close();
            }
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}