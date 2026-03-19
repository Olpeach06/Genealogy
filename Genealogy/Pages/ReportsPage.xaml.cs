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
    public partial class ReportsPage : Page
    {
        private int currentTreeId = 1;
        private List<TreeItem> trees = new List<TreeItem>();

        // Класс для элемента дерева в комбобоксе
        public class TreeItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        // Класс для поколения
        public class GenerationItem
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public int MaxCount { get; set; }
            public string CountText { get; set; }
        }

        // Класс для имени
        public class NameItem
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

        // Класс для события
        public class EventItem
        {
            public string Date { get; set; }
            public string Description { get; set; }
        }

        public ReportsPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Загружаем данные сессии
            if (Session.IsGuest)
                txtUserName.Text = "Гость";
            else
                txtUserName.Text = Session.Username;

            currentTreeId = Session.CurrentTreeId;

            // Показываем кнопки экспорта для админа/редактора
            bool canEdit = Session.IsAdmin || Session.IsEditor;
            panelExport.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;

            LoadTrees();
            LoadReports();
        }

        private void LoadTrees()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var userTrees = context.FamilyTrees
                        .Where(t => t.CreatedByUserId == Session.UserId)
                        .OrderBy(t => t.Name)
                        .ToList();

                    trees.Clear();
                    foreach (var tree in userTrees)
                    {
                        trees.Add(new TreeItem
                        {
                            Id = tree.Id,
                            Name = tree.Name
                        });
                    }

                    cmbTrees.ItemsSource = trees;

                    if (trees.Any())
                    {
                        cmbTrees.SelectedItem = trees.FirstOrDefault(t => t.Id == currentTreeId) ?? trees.First();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка деревьев: {ex.Message}");
            }
        }

        private void LoadReports()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    // Получаем всех персон текущего дерева
                    var persons = context.Persons
                        .Where(p => p.TreeId == currentTreeId)
                        .ToList();

                    if (!persons.Any())
                    {
                        ShowEmptyReports();
                        return;
                    }

                    var personIds = persons.Select(p => p.Id).ToList();

                    // Получаем все связи
                    var relationships = context.Relationships
                        .Where(r => personIds.Contains(r.Person1Id) && personIds.Contains(r.Person2Id))
                        .ToList();

                    // 1. ОБЩАЯ СТАТИСТИКА
                    int totalPersons = persons.Count;
                    int totalDeceased = persons.Count(p => p.DeathDate.HasValue);

                    // Количество семей (уникальные пары родителей)
                    var families = relationships
                        .Where(r => r.RelationshipType == 1)
                        .GroupBy(r => r.Person1Id)
                        .Select(g => g.Key)
                        .Count();

                    int totalMarriages = relationships.Count(r => r.RelationshipType == 2) / 2; // Каждый брак учитывается дважды

                    txtTotalPersons.Text = totalPersons.ToString();
                    txtTotalFamilies.Text = families.ToString();
                    txtTotalMarriages.Text = totalMarriages.ToString();
                    txtTotalDeceased.Text = totalDeceased.ToString();

                    // 2. ДЕМОГРАФИЯ (пол)
                    int menCount = persons.Count(p => p.GenderId == 1);
                    int womenCount = persons.Count(p => p.GenderId == 2);

                    double menPercent = totalPersons > 0 ? (menCount * 100.0 / totalPersons) : 0;
                    double womenPercent = totalPersons > 0 ? (womenCount * 100.0 / totalPersons) : 0;

                    progressMen.Value = menPercent;
                    progressWomen.Value = womenPercent;
                    txtMenPercent.Text = $"{menPercent:F1}%";
                    txtWomenPercent.Text = $"{womenPercent:F1}%";

                    // 3. ВОЗРАСТНАЯ СТАТИСТИКА
                    var livingPersons = persons.Where(p => !p.DeathDate.HasValue && p.BirthDate.HasValue).ToList();

                    if (livingPersons.Any())
                    {
                        int totalAge = 0;
                        foreach (var p in livingPersons)
                        {
                            int age = DateTime.Now.Year - p.BirthDate.Value.Year;
                            if (DateTime.Now < p.BirthDate.Value.AddYears(age)) age--;
                            totalAge += age;
                        }
                        txtAverageAge.Text = $"{totalAge / livingPersons.Count} лет";
                    }
                    else
                    {
                        txtAverageAge.Text = "—";
                    }

                    // Самый старший
                    var oldest = persons.Where(p => p.BirthDate.HasValue)
                                       .OrderBy(p => p.BirthDate)
                                       .FirstOrDefault();
                    if (oldest != null)
                    {
                        int age = DateTime.Now.Year - oldest.BirthDate.Value.Year;
                        if (DateTime.Now < oldest.BirthDate.Value.AddYears(age)) age--;
                        txtOldestPerson.Text = $"{oldest.FirstName} {oldest.LastName} ({age} лет)";
                    }

                    // Самый младший
                    var youngest = persons.Where(p => p.BirthDate.HasValue)
                                        .OrderByDescending(p => p.BirthDate)
                                        .FirstOrDefault();
                    if (youngest != null)
                    {
                        int age = DateTime.Now.Year - youngest.BirthDate.Value.Year;
                        if (DateTime.Now < youngest.BirthDate.Value.AddYears(age)) age--;
                        txtYoungestPerson.Text = $"{youngest.FirstName} {youngest.LastName} ({age} лет)";
                    }

                    // 4. ПОКОЛЕНИЯ
                    var generations = persons
                        .GroupBy(p => GetGenerationLevel(p, relationships))
                        .OrderBy(g => g.Key)
                        .ToList();

                    int maxGenCount = generations.Any() ? generations.Max(g => g.Count()) : 0;
                    var generationItems = new List<GenerationItem>();

                    foreach (var gen in generations)
                    {
                        string genName = gen.Key == 0 ? "Поколение 1 (старшее)" :
                                        gen.Key == 1 ? "Поколение 2" :
                                        gen.Key == 2 ? "Поколение 3" :
                                        gen.Key == 3 ? "Поколение 4" : $"Поколение {gen.Key + 1}";

                        generationItems.Add(new GenerationItem
                        {
                            Name = genName,
                            Count = gen.Count(),
                            MaxCount = maxGenCount,
                            CountText = $"{gen.Count()} чел."
                        });
                    }

                    txtTotalGenerations.Text = $"Всего поколений: {generations.Count()}";
                    lvGenerations.ItemsSource = generationItems;

                    // 5. БЛИЖАЙШИЕ СОБЫТИЯ (дни рождения)
                    var events = new List<EventItem>();
                    var today = DateTime.Today;

                    foreach (var person in livingPersons)
                    {
                        if (person.BirthDate.HasValue)
                        {
                            var nextBirthday = new DateTime(today.Year, person.BirthDate.Value.Month, person.BirthDate.Value.Day);
                            if (nextBirthday < today)
                                nextBirthday = nextBirthday.AddYears(1);

                            int daysUntil = (nextBirthday - today).Days;
                            if (daysUntil <= 30) // Ближайшие 30 дней
                            {
                                int age = nextBirthday.Year - person.BirthDate.Value.Year;
                                events.Add(new EventItem
                                {
                                    Date = nextBirthday.ToString("dd.MM"),
                                    Description = $"День рождения {person.FirstName} {person.LastName} — {age} лет"
                                });
                            }
                        }
                    }

                    events = events.OrderBy(e => DateTime.ParseExact(e.Date + "." + today.Year, "dd.MM.yyyy", null)).ToList();

                    if (events.Any())
                    {
                        lvEvents.ItemsSource = events;
                        txtNoEvents.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        lvEvents.ItemsSource = null;
                        txtNoEvents.Visibility = Visibility.Visible;
                    }

                    // 6. ПОПУЛЯРНЫЕ ИМЕНА
                    var maleNames = persons.Where(p => p.GenderId == 1)
                                          .GroupBy(p => p.FirstName)
                                          .Select(g => new NameItem { Name = g.Key, Count = g.Count() })
                                          .OrderByDescending(n => n.Count)
                                          .Take(5)
                                          .ToList();

                    var femaleNames = persons.Where(p => p.GenderId == 2)
                                            .GroupBy(p => p.FirstName)
                                            .Select(g => new NameItem { Name = g.Key, Count = g.Count() })
                                            .OrderByDescending(n => n.Count)
                                            .Take(5)
                                            .ToList();

                    lvMaleNames.ItemsSource = maleNames;
                    lvFemaleNames.ItemsSource = femaleNames;

                    // Популярные фамилии
                    var surnames = persons.GroupBy(p => p.LastName)
                                          .Select(g => new NameItem { Name = g.Key, Count = g.Count() })
                                          .OrderByDescending(n => n.Count)
                                          .Take(5)
                                          .ToList();

                    lvSurnames.ItemsSource = surnames;

                    // 7. МЕДИА-СТАТИСТИКА
                    var mediaLinks = context.MediaLinks
                        .Where(ml => personIds.Contains(ml.PersonId ?? 0))
                        .Select(ml => ml.MediaFileId)
                        .ToList();

                    var mediaFiles = context.MediaFiles
                        .Where(mf => mediaLinks.Contains(mf.Id))
                        .ToList();

                    int totalMedia = mediaFiles.Count;
                    int photoCount = mediaFiles.Count(mf => mf.MediaTypeId == 1);
                    int videoCount = mediaFiles.Count(mf => mf.MediaTypeId == 2);
                    int audioCount = mediaFiles.Count(mf => mf.MediaTypeId == 3);

                    txtTotalMedia.Text = totalMedia.ToString();
                    txtPhotoCount.Text = photoCount.ToString();
                    txtVideoCount.Text = videoCount.ToString();
                    txtAudioCount.Text = audioCount.ToString();

                    // Персона с наибольшим количеством медиа
                    var topPerson = persons
                        .Select(p => new
                        {
                            Person = p,
                            MediaCount = context.MediaLinks.Count(ml => ml.PersonId == p.Id)
                        })
                        .OrderByDescending(x => x.MediaCount)
                        .FirstOrDefault();

                    if (topPerson != null && topPerson.MediaCount > 0)
                    {
                        txtTopMediaPerson.Text = $"Больше всего медиа у: {topPerson.Person.FirstName} {topPerson.Person.LastName} ({topPerson.MediaCount} файлов)";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки отчетов: {ex.Message}");
            }
        }

        private int GetGenerationLevel(Persons person, List<Relationships> relationships)
        {
            // Определяем поколение по году рождения или по связям
            if (person.BirthDate != null)
            {
                int year = person.BirthDate.Value.Year;
                if (year < 1950) return 0;
                if (year < 1980) return 1;
                if (year < 2000) return 2;
                if (year < 2020) return 3;
                return 4;
            }

            // Если нет даты рождения, пытаемся определить по связям
            var parents = relationships
                .Where(r => r.Person2Id == person.Id && r.RelationshipType == 1)
                .Select(r => r.Person1Id)
                .ToList();

            if (parents.Any())
            {
                // Есть родители - значит не самое старшее поколение
                return 1;
            }

            return 0; // По умолчанию - старшее поколение
        }

        private void ShowEmptyReports()
        {
            txtTotalPersons.Text = "0";
            txtTotalFamilies.Text = "0";
            txtTotalMarriages.Text = "0";
            txtTotalDeceased.Text = "0";

            progressMen.Value = 0;
            progressWomen.Value = 0;
            txtMenPercent.Text = "0%";
            txtWomenPercent.Text = "0%";

            txtAverageAge.Text = "—";
            txtOldestPerson.Text = "—";
            txtYoungestPerson.Text = "—";

            txtTotalGenerations.Text = "Всего поколений: 0";
            lvGenerations.ItemsSource = null;

            lvEvents.ItemsSource = null;
            txtNoEvents.Visibility = Visibility.Visible;

            lvMaleNames.ItemsSource = null;
            lvFemaleNames.ItemsSource = null;
            lvSurnames.ItemsSource = null;

            txtTotalMedia.Text = "0";
            txtPhotoCount.Text = "0";
            txtVideoCount.Text = "0";
            txtAudioCount.Text = "0";
            txtTopMediaPerson.Text = "Больше всего медиа у: —";
        }

        // ==================== ОБРАБОТЧИКИ ====================

        private void TreeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTrees.SelectedItem != null)
            {
                var selectedTree = cmbTrees.SelectedItem as TreeItem;
                if (selectedTree != null)
                {
                    currentTreeId = selectedTree.Id;
                    LoadReports();
                }
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadReports();
        }

        private void MainPageButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new MainPage());
        }

        private void TreesButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new TreesPage());
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            Session.Clear();
            NavigationService.Navigate(new LoginPage());
        }

        // ==================== ЭКСПОРТ (ЗАГЛУШКИ) ====================

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция экспорта в PDF будет доступна в следующей версии",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция экспорта в Excel будет доступна в следующей версии",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция печати будет доступна в следующей версии",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CalendarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string treeName = "Не выбрано";

                using (var context = new GenealogyDBEntities())
                {
                    var tree = context.FamilyTrees.FirstOrDefault(t => t.Id == currentTreeId);
                    if (tree != null)
                    {
                        treeName = tree.Name;
                    }
                }

                var calendarWindow = new CalendarWindow(currentTreeId, treeName)
                {
                    Owner = Window.GetWindow(this)
                };
                calendarWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия календаря: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
