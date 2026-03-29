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
using Microsoft.Win32;
using System.IO;

namespace Genealogy.Pages
{
    public partial class ReportsPage : Page
    {
        private int currentTreeId = 1;
        private List<TreeItem> trees = new List<TreeItem>();
        private List<Persons> currentPersons = new List<Persons>();
        private List<Relationships> currentRelationships = new List<Relationships>();

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
            {
                txtUserName.Text = "Гость";
                btnUsers.Visibility = Visibility.Collapsed; // Скрываем для гостя
            }
            else
            {
                txtUserName.Text = Session.Username;
            }

            currentTreeId = Session.CurrentTreeId;

            // Показываем кнопки экспорта и кнопку Пользователи для админа/редактора
            bool canEdit = Session.IsAdmin || Session.IsEditor;
            panelExport.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnUsers.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;

            LoadTrees();
            LoadReports();
        }

        // Метод для кнопки Пользователи
        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new UsersPage());
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
                    currentPersons = context.Persons
                        .Where(p => p.TreeId == currentTreeId)
                        .ToList();

                    if (!currentPersons.Any())
                    {
                        ShowEmptyReports();
                        return;
                    }

                    var personIds = currentPersons.Select(p => p.Id).ToList();

                    // Получаем все связи
                    currentRelationships = context.Relationships
                        .Where(r => personIds.Contains(r.Person1Id) && personIds.Contains(r.Person2Id))
                        .ToList();

                    // 1. ОБЩАЯ СТАТИСТИКА
                    int totalPersons = currentPersons.Count;
                    int totalDeceased = currentPersons.Count(p => p.DeathDate.HasValue);

                    // Количество семей (уникальные пары родителей)
                    var families = currentRelationships
                        .Where(r => r.RelationshipType == 1)
                        .GroupBy(r => r.Person1Id)
                        .Select(g => g.Key)
                        .Count();

                    int totalMarriages = currentRelationships.Count(r => r.RelationshipType == 2) / 2;

                    txtTotalPersons.Text = totalPersons.ToString();
                    txtTotalFamilies.Text = families.ToString();
                    txtTotalMarriages.Text = totalMarriages.ToString();
                    txtTotalDeceased.Text = totalDeceased.ToString();

                    // 2. ДЕМОГРАФИЯ (пол)
                    int menCount = currentPersons.Count(p => p.GenderId == 1);
                    int womenCount = currentPersons.Count(p => p.GenderId == 2);

                    double menPercent = totalPersons > 0 ? (menCount * 100.0 / totalPersons) : 0;
                    double womenPercent = totalPersons > 0 ? (womenCount * 100.0 / totalPersons) : 0;

                    progressMen.Value = menPercent;
                    progressWomen.Value = womenPercent;
                    txtMenPercent.Text = $"{menPercent:F1}%";
                    txtWomenPercent.Text = $"{womenPercent:F1}%";

                    // 3. ВОЗРАСТНАЯ СТАТИСТИКА
                    var livingPersons = currentPersons.Where(p => !p.DeathDate.HasValue && p.BirthDate.HasValue).ToList();

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
                    var oldest = currentPersons.Where(p => p.BirthDate.HasValue)
                                       .OrderBy(p => p.BirthDate)
                                       .FirstOrDefault();
                    if (oldest != null)
                    {
                        int age = DateTime.Now.Year - oldest.BirthDate.Value.Year;
                        if (DateTime.Now < oldest.BirthDate.Value.AddYears(age)) age--;
                        txtOldestPerson.Text = $"{oldest.FirstName} {oldest.LastName} ({age} лет)";
                    }

                    // Самый младший
                    var youngest = currentPersons.Where(p => p.BirthDate.HasValue)
                                        .OrderByDescending(p => p.BirthDate)
                                        .FirstOrDefault();
                    if (youngest != null)
                    {
                        int age = DateTime.Now.Year - youngest.BirthDate.Value.Year;
                        if (DateTime.Now < youngest.BirthDate.Value.AddYears(age)) age--;
                        txtYoungestPerson.Text = $"{youngest.FirstName} {youngest.LastName} ({age} лет)";
                    }

                    // 4. ПОКОЛЕНИЯ
                    var generations = currentPersons
                        .GroupBy(p => GetGenerationLevel(p, currentRelationships))
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
                            if (daysUntil <= 30)
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
                    var maleNames = currentPersons.Where(p => p.GenderId == 1)
                                          .GroupBy(p => p.FirstName)
                                          .Select(g => new NameItem { Name = g.Key, Count = g.Count() })
                                          .OrderByDescending(n => n.Count)
                                          .Take(5)
                                          .ToList();

                    var femaleNames = currentPersons.Where(p => p.GenderId == 2)
                                            .GroupBy(p => p.FirstName)
                                            .Select(g => new NameItem { Name = g.Key, Count = g.Count() })
                                            .OrderByDescending(n => n.Count)
                                            .Take(5)
                                            .ToList();

                    lvMaleNames.ItemsSource = maleNames;
                    lvFemaleNames.ItemsSource = femaleNames;

                    // Популярные фамилии
                    var surnames = currentPersons.GroupBy(p => p.LastName)
                                          .Select(g => new NameItem { Name = g.Key, Count = g.Count() })
                                          .OrderByDescending(n => n.Count)
                                          .Take(5)
                                          .ToList();

                    lvSurnames.ItemsSource = surnames;

                    // 7. МЕДИА-СТАТИСТИКА
                    var stories = context.Stories
                        .Where(s => personIds.Contains(s.PersonId))
                        .Select(s => s.Id)
                        .ToList();

                    var mediaLinks = context.MediaLinks
                        .Where(ml => ml.StoryId.HasValue && stories.Contains(ml.StoryId.Value))
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

                    // Персона с наибольшим количеством медиафайлов
                    var personMediaCounts = currentPersons.Select(p => new
                    {
                        Person = p,
                        MediaCount = context.MediaLinks
                            .Where(ml => context.Stories
                                .Where(s => s.PersonId == p.Id)
                                .Select(s => s.Id)
                                .Contains(ml.StoryId ?? 0))
                            .Count()
                    })
                    .OrderByDescending(x => x.MediaCount)
                    .ToList();

                    var topPerson = personMediaCounts.FirstOrDefault();
                    if (topPerson != null && topPerson.MediaCount > 0)
                    {
                        txtTopMediaPerson.Text = $"Больше всего медиа у: {topPerson.Person.FirstName} {topPerson.Person.LastName} ({topPerson.MediaCount} файлов)";
                    }
                    else
                    {
                        txtTopMediaPerson.Text = "Больше всего медиа у: —";
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
            if (person.BirthDate != null)
            {
                int year = person.BirthDate.Value.Year;
                if (year < 1950) return 0;
                if (year < 1980) return 1;
                if (year < 2000) return 2;
                if (year < 2020) return 3;
                return 4;
            }

            var parents = relationships
                .Where(r => r.Person2Id == person.Id && r.RelationshipType == 1)
                .Select(r => r.Person1Id)
                .ToList();

            if (parents.Any())
            {
                return 1;
            }

            return 0;
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

        // ==================== ЭКСПОРТ ====================

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            ExportToTxt();
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportToCsv();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            ExportToTxt();
        }

        private void ExportToCsv()
        {
            try
            {
                if (!currentPersons.Any())
                {
                    MessageBox.Show("Нет данных для экспорта", "Экспорт",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv",
                    FileName = $"Отчет_по_дереву_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();

                    // Заголовки столбцов
                    sb.AppendLine("ID;Фамилия;Имя;Отчество;Дата рождения;Дата смерти;Пол;Место рождения;Место смерти;Биография");

                    // Данные
                    foreach (var person in currentPersons)
                    {
                        string gender = "";
                        if (person.GenderId == 1) gender = "Мужской";
                        else if (person.GenderId == 2) gender = "Женский";
                        else gender = "Не указан";

                        sb.AppendLine($"{person.Id};{person.LastName};{person.FirstName};{person.Patronymic};{person.BirthDate?.ToString("dd.MM.yyyy")};{person.DeathDate?.ToString("dd.MM.yyyy")};{gender};{person.BirthPlace};{person.DeathPlace};{person.Biography?.Replace(";", ",")}");
                    }

                    System.IO.File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                    MessageBox.Show($"Файл успешно сохранён!\nВсего экспортировано: {currentPersons.Count} записей",
                        "Экспорт в CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в CSV: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToTxt()
        {
            try
            {
                if (!currentPersons.Any())
                {
                    MessageBox.Show("Нет данных для экспорта", "Экспорт",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "Текстовые файлы (*.txt)|*.txt",
                    FileName = $"Отчет_по_дереву_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var writer = new StreamWriter(saveDialog.FileName, false, Encoding.UTF8))
                    {
                        // Получаем название текущего дерева
                        string treeName = trees.FirstOrDefault(t => t.Id == currentTreeId)?.Name ?? "Неизвестно";

                        writer.WriteLine($"ГЕНЕАЛОГИЧЕСКИЙ ОТЧЕТ");
                        writer.WriteLine($"Древо: {treeName}");
                        writer.WriteLine($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                        writer.WriteLine($"Всего персон в древе: {currentPersons.Count}");
                        writer.WriteLine(new string('=', 80));
                        writer.WriteLine();

                        // Общая статистика
                        int totalDeceased = currentPersons.Count(p => p.DeathDate.HasValue);
                        int menCount = currentPersons.Count(p => p.GenderId == 1);
                        int womenCount = currentPersons.Count(p => p.GenderId == 2);

                        writer.WriteLine("ОБЩАЯ СТАТИСТИКА");
                        writer.WriteLine(new string('-', 40));
                        writer.WriteLine($"Всего персон: {currentPersons.Count}");
                        writer.WriteLine($"Мужчин: {menCount}");
                        writer.WriteLine($"Женщин: {womenCount}");
                        writer.WriteLine($"Умерших: {totalDeceased}");
                        writer.WriteLine();

                        // Возрастная статистика
                        var livingPersons = currentPersons.Where(p => !p.DeathDate.HasValue && p.BirthDate.HasValue).ToList();
                        if (livingPersons.Any())
                        {
                            int totalAge = 0;
                            foreach (var p in livingPersons)
                            {
                                int age = DateTime.Now.Year - p.BirthDate.Value.Year;
                                if (DateTime.Now < p.BirthDate.Value.AddYears(age)) age--;
                                totalAge += age;
                            }
                            writer.WriteLine("ВОЗРАСТНАЯ СТАТИСТИКА");
                            writer.WriteLine(new string('-', 40));
                            writer.WriteLine($"Средний возраст: {totalAge / livingPersons.Count} лет");

                            var oldest = currentPersons.Where(p => p.BirthDate.HasValue).OrderBy(p => p.BirthDate).FirstOrDefault();
                            if (oldest != null)
                            {
                                int age = DateTime.Now.Year - oldest.BirthDate.Value.Year;
                                if (DateTime.Now < oldest.BirthDate.Value.AddYears(age)) age--;
                                writer.WriteLine($"Самый старший: {oldest.FirstName} {oldest.LastName} ({age} лет)");
                            }

                            var youngest = currentPersons.Where(p => p.BirthDate.HasValue).OrderByDescending(p => p.BirthDate).FirstOrDefault();
                            if (youngest != null)
                            {
                                int age = DateTime.Now.Year - youngest.BirthDate.Value.Year;
                                if (DateTime.Now < youngest.BirthDate.Value.AddYears(age)) age--;
                                writer.WriteLine($"Самый младший: {youngest.FirstName} {youngest.LastName} ({age} лет)");
                            }
                            writer.WriteLine();
                        }

                        // Список всех персон
                        writer.WriteLine("СПИСОК ВСЕХ ПЕРСОН");
                        writer.WriteLine(new string('-', 80));
                        writer.WriteLine();

                        foreach (var person in currentPersons.OrderBy(p => p.LastName).ThenBy(p => p.FirstName))
                        {
                            writer.WriteLine($"ID: {person.Id}");
                            writer.WriteLine($"ФИО: {person.LastName} {person.FirstName} {person.Patronymic}");
                            writer.WriteLine($"Дата рождения: {person.BirthDate?.ToString("dd.MM.yyyy") ?? "не указана"}");
                            writer.WriteLine($"Дата смерти: {person.DeathDate?.ToString("dd.MM.yyyy") ?? "—"}");

                            string gender = "";
                            if (person.GenderId == 1) gender = "Мужской";
                            else if (person.GenderId == 2) gender = "Женский";
                            else gender = "Не указан";
                            writer.WriteLine($"Пол: {gender}");

                            if (!string.IsNullOrEmpty(person.BirthPlace))
                                writer.WriteLine($"Место рождения: {person.BirthPlace}");
                            if (!string.IsNullOrEmpty(person.DeathPlace))
                                writer.WriteLine($"Место смерти: {person.DeathPlace}");
                            if (!string.IsNullOrEmpty(person.Biography))
                                writer.WriteLine($"Биография: {person.Biography}");

                            writer.WriteLine(new string('-', 40));
                        }

                        writer.WriteLine();
                        writer.WriteLine(new string('=', 80));
                        writer.WriteLine($"Отчет сгенерирован автоматически в {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                    }

                    MessageBox.Show($"Файл успешно сохранён!\nВсего экспортировано: {currentPersons.Count} записей",
                        "Экспорт в TXT", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в TXT: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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