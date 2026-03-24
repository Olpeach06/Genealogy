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
    public partial class LinkUserToPersonPage : Page
    {
        private int userId;

        public class PersonItem
        {
            public int Id { get; set; }
            public string DisplayName { get; set; }
        }

        public LinkUserToPersonPage(int userId)
        {
            InitializeComponent();
            this.userId = userId;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUserInfo();
            LoadPersons();
        }

        private void LoadUserInfo()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var user = context.Users.Find(userId);
                    if (user != null)
                    {
                        txtUserName.Text = $"{user.Username} (ID: {user.Id})";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки пользователя: {ex.Message}");
            }
        }

        private void LoadPersons()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    // Получаем все деревья, доступные пользователю
                    var userTrees = context.FamilyTrees
                        .Where(t => t.CreatedByUserId == userId)
                        .Select(t => t.Id)
                        .ToList();

                    // Если нет своих деревьев, берем все публичные деревья или деревья, созданные администратором
                    if (!userTrees.Any())
                    {
                        // Получаем все деревья, которые может видеть пользователь
                        // Для простоты берем все деревья
                        userTrees = context.FamilyTrees
                            .Select(t => t.Id)
                            .ToList();
                    }

                    // Получаем персоны из доступных деревьев
                    var personsQuery = context.Persons
                        .Where(p => userTrees.Contains(p.TreeId))
                        .ToList(); // Загружаем в память, чтобы выполнить форматирование на клиенте

                    var persons = personsQuery.Select(p => new PersonItem
                    {
                        Id = p.Id,
                        DisplayName = $"{p.LastName} {p.FirstName} {p.Patronymic} ({p.BirthDate?.Year})".Trim()
                    })
                    .OrderBy(p => p.DisplayName)
                    .ToList();

                    persons.Insert(0, new PersonItem { Id = 0, DisplayName = "— Не выбрано —" });

                    cmbPerson.ItemsSource = persons;
                    cmbPerson.SelectedValuePath = "Id";
                    cmbPerson.DisplayMemberPath = "DisplayName";

                    // Если у пользователя уже есть привязанная персона, выбираем её
                    var user = context.Users.Find(userId);
                    if (user?.PersonId.HasValue == true)
                    {
                        // Проверяем, существует ли эта персона в списке
                        var existingPerson = persons.FirstOrDefault(p => p.Id == user.PersonId.Value);
                        if (existingPerson != null)
                        {
                            cmbPerson.SelectedValue = user.PersonId.Value;
                        }
                        else
                        {
                            cmbPerson.SelectedIndex = 0;
                        }
                    }
                    else
                    {
                        cmbPerson.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки персон: {ex.Message}");
            }
        }

        private void ClearLinkButton_Click(object sender, RoutedEventArgs e)
        {
            cmbPerson.SelectedIndex = 0;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int? selectedPersonId = null;
                if (cmbPerson.SelectedValue != null && (int)cmbPerson.SelectedValue > 0)
                {
                    selectedPersonId = (int)cmbPerson.SelectedValue;
                }

                using (var context = new GenealogyDBEntities())
                {
                    var user = context.Users.Find(userId);
                    if (user != null)
                    {
                        user.PersonId = selectedPersonId;
                        context.SaveChanges();

                        string message = selectedPersonId.HasValue
                            ? "Пользователь привязан к персоне!"
                            : "Привязка пользователя удалена!";

                        MessageBox.Show(message, "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                NavigationService.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}