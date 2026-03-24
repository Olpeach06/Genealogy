using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Genealogy.Classes;
using Genealogy.AppData;

namespace Genealogy.Pages
{
    public partial class UsersPage : Page
    {
        private List<UserItem> allUsers = new List<UserItem>();
        private List<RoleItem> roles = new List<RoleItem>();
        private string currentSearchText = "";
        private bool isPageLoaded = false;

        public class UserItem
        {
            public int Id { get; set; }
            public string Username { get; set; }
            public string Email { get; set; }
            public int RoleId { get; set; }
            public string RoleName { get; set; }
            public int? PersonId { get; set; }
            public string PersonName { get; set; }
            public bool IsActive { get; set; }
            public string CreatedDate { get; set; }
            public string LastLoginDate { get; set; }
        }

        public class RoleItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public List<RoleItem> Roles => roles;
        public bool CanEdit => Session.IsAdmin || Session.IsEditor;
        public bool IsAdmin => Session.IsAdmin;

        public UsersPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            DataContext = this;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Проверка прав доступа
            if (!Session.IsAdmin && !Session.IsEditor)
            {
                MessageBox.Show("У вас нет доступа к этой странице!", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NavigationService.GoBack();
                return;
            }

            // Загружаем данные сессии
            if (Session.IsGuest)
                txtUserName.Text = "Гость";
            else
                txtUserName.Text = Session.Username;

            // Кнопка добавления видна только администратору
            if (btnAddUser != null)
                btnAddUser.Visibility = Session.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

            LoadRoles();
            LoadUsers();

            isPageLoaded = true;
        }

        private void LoadRoles()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    roles = context.UserRoles
                        .Select(r => new RoleItem
                        {
                            Id = r.Id,
                            Name = r.Name
                        })
                        .OrderBy(r => r.Id)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки ролей: {ex.Message}");
            }
        }

        private void LoadUsers()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var users = context.Users
                        .OrderBy(u => u.Id)
                        .ToList();

                    var personIds = users.Where(u => u.PersonId.HasValue)
                                         .Select(u => u.PersonId.Value)
                                         .ToList();

                    var persons = new Dictionary<int, string>();
                    if (personIds.Any())
                    {
                        persons = context.Persons
                            .Where(p => personIds.Contains(p.Id))
                            .ToDictionary(p => p.Id, p => $"{p.FirstName} {p.LastName}");
                    }

                    allUsers = users.Select(u => new UserItem
                    {
                        Id = u.Id,
                        Username = u.Username,
                        Email = u.Email ?? "—",
                        RoleId = u.RoleId,
                        RoleName = roles.FirstOrDefault(r => r.Id == u.RoleId)?.Name ?? "—",
                        PersonId = u.PersonId,
                        PersonName = u.PersonId.HasValue && persons.ContainsKey(u.PersonId.Value)
                            ? persons[u.PersonId.Value]
                            : "—",
                        IsActive = u.IsActive,
                        CreatedDate = u.CreatedAt.ToString("dd.MM.yyyy"),
                        LastLoginDate = u.LastLoginAt?.ToString("dd.MM.yyyy HH:mm") ?? "—"
                    }).ToList();

                    ApplyFilter();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки пользователей: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            if (lvUsers == null || !isPageLoaded)
                return;

            var filtered = allUsers;

            if (!string.IsNullOrWhiteSpace(currentSearchText))
            {
                filtered = filtered.Where(u =>
                    u.Username.ToLower().Contains(currentSearchText) ||
                    (u.Email?.ToLower().Contains(currentSearchText) ?? false) ||
                    u.PersonName.ToLower().Contains(currentSearchText)
                ).ToList();
            }

            lvUsers.ItemsSource = filtered;

            if (txtUsersCount != null)
                txtUsersCount.Text = $"Всего пользователей: {filtered.Count}";
        }

        // ==================== ОБРАБОТЧИКИ ПОИСКА ====================

        private void txtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSearch.Text == "Поиск...")
                txtSearch.Text = "";
        }

        private void txtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
                txtSearch.Text = "Поиск...";
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformSearch();
                e.Handled = true;
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isPageLoaded) return;

            // Показываем/скрываем кнопку очистки
            if (btnClearSearch != null)
            {
                if (txtSearch.Text != "Поиск..." && !string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    btnClearSearch.Visibility = Visibility.Visible;
                }
                else
                {
                    btnClearSearch.Visibility = Visibility.Collapsed;
                }
            }

            // Выполняем поиск при вводе текста
            if (txtSearch.Text != "Поиск..." && !string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                currentSearchText = txtSearch.Text.ToLower();
            }
            else
            {
                currentSearchText = "";
            }
            ApplyFilter();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            if (txtSearch != null)
                txtSearch.Text = "Поиск...";

            currentSearchText = "";
            ApplyFilter();

            if (btnClearSearch != null)
                btnClearSearch.Visibility = Visibility.Collapsed;
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void PerformSearch()
        {
            if (!isPageLoaded) return;

            if (txtSearch.Text != "Поиск..." && !string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                currentSearchText = txtSearch.Text.ToLower();
            }
            else
            {
                currentSearchText = "";
            }
            ApplyFilter();
        }

        // ==================== ОБРАБОТЧИКИ ДЕЙСТВИЙ ====================

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            int userId = (int)button.Tag;
            NavigationService.Navigate(new EditUserPage(userId));
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            int userId = (int)button.Tag;

            if (!Session.IsAdmin)
            {
                MessageBox.Show("Только администратор может удалять пользователей!",
                    "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (userId == Session.UserId)
            {
                MessageBox.Show("Нельзя удалить свою учетную запись!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var targetUser = allUsers.FirstOrDefault(u => u.Id == userId);
            if (targetUser == null) return;

            var result = MessageBox.Show($"Вы уверены, что хотите удалить пользователя {targetUser.Username}?\n" +
                "Все связанные данные (деревья, персоны, истории) будут также удалены!",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new GenealogyDBEntities())
                    {
                        var userTrees = context.FamilyTrees.Where(t => t.CreatedByUserId == userId).ToList();

                        foreach (var tree in userTrees)
                        {
                            var persons = context.Persons.Where(p => p.TreeId == tree.Id).ToList();
                            var personIds = persons.Select(p => p.Id).ToList();

                            var mediaLinks = context.MediaLinks.Where(ml => personIds.Contains(ml.PersonId ?? 0)).ToList();
                            context.MediaLinks.RemoveRange(mediaLinks);

                            var stories = context.Stories.Where(s => personIds.Contains(s.PersonId)).ToList();
                            context.Stories.RemoveRange(stories);

                            var relationships = context.Relationships
                                .Where(r => personIds.Contains(r.Person1Id) || personIds.Contains(r.Person2Id))
                                .ToList();
                            context.Relationships.RemoveRange(relationships);

                            context.Persons.RemoveRange(persons);
                        }

                        context.FamilyTrees.RemoveRange(userTrees);

                        var user = context.Users.Find(userId);
                        if (user != null)
                            context.Users.Remove(user);

                        context.SaveChanges();

                        MessageBox.Show("Пользователь удален!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadUsers();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления пользователя: {ex.Message}");
                }
            }
        }

        private void AddUserButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new EditUserPage());
        }

        // ==================== ПРИВЯЗКА К ПЕРСОНЕ ====================

        private void LinkPerson_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            int userId = (int)button.Tag;

            // Только администратор и редактор могут привязывать
            if (!(Session.IsAdmin || Session.IsEditor))
            {
                MessageBox.Show("У вас нет прав для привязки пользователя к персоне!",
                    "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NavigationService.Navigate(new LinkUserToPersonPage(userId));
        }

        private void UserDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selectedUser = lvUsers?.SelectedItem as UserItem;
            if (selectedUser != null)
            {
                if (Session.IsAdmin || Session.IsEditor)
                {
                    NavigationService.Navigate(new EditUserPage(selectedUser.Id));
                }
                else
                {
                    MessageBox.Show($"Пользователь: {selectedUser.Username}\n" +
                                    $"Email: {selectedUser.Email}\n" +
                                    $"Роль: {selectedUser.RoleName}\n" +
                                    $"Активен: {(selectedUser.IsActive ? "Да" : "Нет")}",
                                    "Информация о пользователе",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // ==================== НАВИГАЦИЯ ====================

        private void MainPageButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new MainPage());
        }

        private void TreesButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new TreesPage());
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ReportsPage());
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            Session.Clear();
            NavigationService.Navigate(new LoginPage());
        }
    }
}