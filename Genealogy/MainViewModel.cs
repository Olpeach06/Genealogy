using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Genealogy.Classes
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _userName;
        private bool _isGuest;
        private bool _isAdmin;
        private bool _isEditor;
        private string _searchText = "Поиск...";

        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }

        public bool IsGuest
        {
            get => _isGuest;
            set { _isGuest = value; OnPropertyChanged(); }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set { _isAdmin = value; OnPropertyChanged(); }
        }

        public bool IsEditor
        {
            get => _isEditor;
            set { _isEditor = value; OnPropertyChanged(); }
        }

        public bool CanEdit => IsAdmin || IsEditor;

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}