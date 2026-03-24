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
    public partial class TreesPage : Page
    {
        private List<TreeItem> allTrees = new List<TreeItem>();

        // Класс для отображения дерева в списке
        public class TreeItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Stats { get; set; }
            public string CreatedDate { get; set; }
            public int PersonsCount { get; set; }
            public int StoriesCount { get; set; }
            public int MediaCount { get; set; }
            public bool IsCurrent { get; set; }
            public bool ShowDeleteButton { get; set; } // Свойство для отображения кнопки удаления
        }

        public TreesPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Загружаем данные сессии
            if (Session.IsGuest)
            {
                txtUserName.Text = "Гость";
                btnCreateTree.Visibility = Visibility.Collapsed;
                btnUsers.Visibility = Visibility.Collapsed; // Скрываем для гостя
            }
            else
            {
                txtUserName.Text = Session.Username;
            }

            // Проверка прав на создание/удаление
            bool canEdit = Session.IsAdmin || Session.IsEditor;
            btnCreateTree.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;

            // Показываем кнопку "Пользователи" для админов и редакторов
            btnUsers.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;

            LoadTrees();
        }

        private void LoadTrees()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    // Получаем все деревья, созданные текущим пользователем
                    var trees = context.FamilyTrees
                        .Where(t => t.CreatedByUserId == Session.UserId)
                        .OrderByDescending(t => t.CreatedAt)
                        .ToList();

                    if (!trees.Any())
                    {
                        // Если нет деревьев, показываем сообщение
                        borderCurrentTree.Visibility = Visibility.Collapsed;
                        borderNoTrees.Visibility = Visibility.Visible;
                        lvTrees.Visibility = Visibility.Collapsed;
                        return;
                    }
                    else
                    {
                        lvTrees.Visibility = Visibility.Visible;
                        borderNoTrees.Visibility = Visibility.Collapsed;
                    }

                    // Формируем список для отображения
                    allTrees.Clear();

                    foreach (var tree in trees)
                    {
                        // Получаем статистику по дереву
                        var personsCount = context.Persons.Count(p => p.TreeId == tree.Id);

                        var personIds = context.Persons
                            .Where(p => p.TreeId == tree.Id)
                            .Select(p => p.Id)
                            .ToList();

                        var storiesCount = context.Stories
                            .Count(s => personIds.Contains(s.PersonId));

                        var mediaCount = context.MediaLinks
                            .Count(ml => personIds.Contains(ml.PersonId ?? 0));

                        bool isCurrent = (tree.Id == Session.CurrentTreeId);

                        allTrees.Add(new TreeItem
                        {
                            Id = tree.Id,
                            Name = tree.Name,
                            Description = tree.Description ?? "Нет описания",
                            Stats = $"👥 {personsCount} персон  |  📖 {storiesCount} историй  |  📷 {mediaCount} фото",
                            CreatedDate = $"Создано: {tree.CreatedAt:dd.MM.yyyy}",
                            PersonsCount = personsCount,
                            StoriesCount = storiesCount,
                            MediaCount = mediaCount,
                            IsCurrent = isCurrent,
                            ShowDeleteButton = Session.IsAdmin // Только администратор видит кнопку удаления
                        });
                    }

                    // Разделяем текущее дерево и остальные
                    var currentTree = allTrees.FirstOrDefault(t => t.IsCurrent);
                    var otherTrees = allTrees.Where(t => !t.IsCurrent).ToList();

                    // Отображаем текущее дерево
                    if (currentTree != null)
                    {
                        borderCurrentTree.Visibility = Visibility.Visible;
                        txtCurrentTreeName.Text = currentTree.Name;
                        txtCurrentTreeDesc.Text = currentTree.Description;
                        txtCurrentTreeStats.Text = currentTree.Stats;
                        txtCurrentTreeDate.Text = currentTree.CreatedDate;

                        btnEditCurrent.Tag = currentTree.Id;
                        btnDeleteCurrent.Tag = currentTree.Id;

                        // Проверка прав на удаление
                        btnDeleteCurrent.Visibility = Session.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        borderCurrentTree.Visibility = Visibility.Collapsed;
                    }

                    // Отображаем остальные деревья
                    if (otherTrees.Any())
                    {
                        lvTrees.ItemsSource = otherTrees;
                    }
                    else
                    {
                        lvTrees.ItemsSource = null;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки деревьев: {ex.Message}");
            }
        }

        // ==================== ОБРАБОТЧИКИ КНОПОК ====================

        private void MainPageButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new MainPage());
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ReportsPage());
        }

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new UsersPage());
        }

        private void CreateTreeButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем диалог создания нового дерева
            var dialog = new TreeEditDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var context = new GenealogyDBEntities())
                    {
                        var newTree = new FamilyTrees
                        {
                            Name = dialog.TreeName,
                            Description = dialog.TreeDescription,
                            CreatedByUserId = Session.UserId,
                            CreatedAt = DateTime.Now,
                            IsPublic = dialog.IsPublic
                        };
                        context.FamilyTrees.Add(newTree);
                        context.SaveChanges();

                        // Если это первое дерево, автоматически делаем его текущим
                        var treeCount = context.FamilyTrees.Count(t => t.CreatedByUserId == Session.UserId);
                        if (treeCount == 1)
                        {
                            Session.CurrentTreeId = newTree.Id;
                        }

                        MessageBox.Show("Новое дерево создано!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        LoadTrees(); // Перезагружаем список
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания дерева: {ex.Message}");
                }
            }
        }

        private void SelectTreeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            int treeId = (int)button.Tag;

            // Сохраняем выбранное дерево в сессии
            Session.CurrentTreeId = treeId;

            // Сразу переходим на главную страницу с выбранным деревом
            NavigationService.Navigate(new MainPage());
        }

        private void EditTreeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            int treeId = (int)button.Tag;

            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var tree = context.FamilyTrees.FirstOrDefault(t => t.Id == treeId);
                    if (tree == null) return;

                    // Открываем диалог редактирования
                    var dialog = new TreeEditDialog(tree);
                    if (dialog.ShowDialog() == true)
                    {
                        tree.Name = dialog.TreeName;
                        tree.Description = dialog.TreeDescription;
                        tree.IsPublic = dialog.IsPublic;
                        context.SaveChanges();

                        MessageBox.Show("Дерево обновлено!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        LoadTrees(); // Перезагружаем список
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка редактирования дерева: {ex.Message}");
            }
        }

        private void DeleteTreeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            int treeId = (int)button.Tag;

            // Проверка прав
            if (!Session.IsAdmin)
            {
                MessageBox.Show("Только администратор может удалять деревья!", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("Вы уверены, что хотите удалить это дерево?\nВсе связанные персоны, истории и медиафайлы будут также удалены!",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new GenealogyDBEntities())
                    {
                        // Получаем всех персон дерева
                        var persons = context.Persons.Where(p => p.TreeId == treeId).ToList();
                        var personIds = persons.Select(p => p.Id).ToList();

                        // Удаляем связи медиафайлов
                        var mediaLinks = context.MediaLinks.Where(ml => personIds.Contains(ml.PersonId ?? 0)).ToList();
                        context.MediaLinks.RemoveRange(mediaLinks);

                        // Удаляем истории
                        var stories = context.Stories.Where(s => personIds.Contains(s.PersonId)).ToList();
                        context.Stories.RemoveRange(stories);

                        // Удаляем связи
                        var relationships = context.Relationships
                            .Where(r => personIds.Contains(r.Person1Id) || personIds.Contains(r.Person2Id))
                            .ToList();
                        context.Relationships.RemoveRange(relationships);

                        // Удаляем персон
                        context.Persons.RemoveRange(persons);

                        // Удаляем само дерево
                        var tree = context.FamilyTrees.FirstOrDefault(t => t.Id == treeId);
                        if (tree != null)
                            context.FamilyTrees.Remove(tree);

                        context.SaveChanges();

                        // Если удалили текущее дерево, сбрасываем CurrentTreeId
                        if (treeId == Session.CurrentTreeId)
                        {
                            // Находим первое доступное дерево
                            var anyTree = context.FamilyTrees
                                .Where(t => t.CreatedByUserId == Session.UserId)
                                .FirstOrDefault();
                            Session.CurrentTreeId = anyTree?.Id ?? 0;
                        }

                        MessageBox.Show("Дерево удалено!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        LoadTrees(); // Перезагружаем список
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления дерева: {ex.Message}");
                }
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            Session.Clear();
            NavigationService.Navigate(new LoginPage());
        }
    }
}