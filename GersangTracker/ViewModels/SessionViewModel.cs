using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GersangTracker.Models;
using GersangTracker.Services;
using System.Collections.ObjectModel;

namespace GersangTracker.ViewModels
{
    public partial class SessionViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly Monster _monster;

        public string MonsterName => _monster.Name;
        public ObservableCollection<Session> Sessions { get; } = new();

        [ObservableProperty]
        private Session? _selectedSession;

        public SessionViewModel(Monster monster)
        {
            _databaseService = new DatabaseService();
            _monster = monster;
        }

        // 세션 목록 불러오기
        public async Task LoadSessionsAsync()
        {
            var sessions = await _databaseService.GetSessionsByMonsterAsync(_monster.Id);
            Sessions.Clear();
            foreach (var session in sessions)
                Sessions.Add(session);
        }

        // 세션 삭제
        [RelayCommand]
        private async Task DeleteSessionAsync(Session session)
        {
            await _databaseService.DeleteSessionAsync(session.Id);
            await LoadSessionsAsync();
        }
    }
}