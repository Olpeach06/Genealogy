using Genealogy.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Genealogy.AppData;

namespace Genealogy.Pages
{
    public partial class PersonProfilePage : Page
    {
        private int personId;
        private List<StoryItem> stories = new List<StoryItem>();

        public class StoryItem
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public DateTime? EventDate { get; set; }
            public string EventDateString { get; set; }
            public string ShortContent { get; set; }
        }

        public class PhotoItem
        {
            public int Id { get; set; }
            public string FilePath { get; set; }
            public string ThumbPath { get; set; }
            public string FileName { get; set; }
        }

        public class VideoItem
        {
            public int Id { get; set; }
            public string FileName { get; set; }
            public string FilePath { get; set; }
        }

        public class AudioItem
        {
            public int Id { get; set; }
            public string FileName { get; set; }
            public string FilePath { get; set; }
        }

        public PersonProfilePage(int id)
        {
            InitializeComponent();
            personId = id;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Сначала очищаем все текстовые поля от заглушек из XAML
            ClearAllTextBlocks();

            // Загружаем данные
            LoadPersonData();
            LoadStories();
            LoadMediaFiles();

            // Проверяем права доступа
            bool canEdit = Session.IsAdmin || Session.IsEditor;
            btnEdit.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddStory.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddStoryBottom.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddPhoto.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddMedia.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;

            // Запускаем диагностику (можно закомментировать после отладки)
            DebugCheckDatabase();
        }

        private void ClearAllTextBlocks()
        {
            txtFather.Text = "";
            txtMother.Text = "";
            txtSpouse.Text = "";
            txtChildren.Text = "";
            txtFullName.Text = "";
            txtBirthDate.Text = "";
            txtDeathDate.Text = "";
            txtBirthPlace.Text = "";
            txtDeathPlace.Text = "";
            txtBiography.Text = "";
            txtGender.Text = "";
            txtGenderSymbol.Text = "";
        }

        private void DebugCheckDatabase()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    System.Diagnostics.Debug.WriteLine("=================================");
                    System.Diagnostics.Debug.WriteLine("=== ДИАГНОСТИКА БАЗЫ ДАННЫХ ===");
                    System.Diagnostics.Debug.WriteLine("=================================");

                    // Проверяем текущую персону
                    var person = context.Persons.FirstOrDefault(p => p.Id == personId);
                    if (person != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Текущая персона: ID={person.Id}, {person.LastName} {person.FirstName} {person.Patronymic}");
                        System.Diagnostics.Debug.WriteLine($"  Пол: {person.GenderId}");
                    }

                    // Все отношения для этой персоны
                    var allRels = context.Relationships
                        .Where(r => r.Person1Id == personId || r.Person2Id == personId)
                        .ToList();

                    System.Diagnostics.Debug.WriteLine($"Всего отношений с участием персоны: {allRels.Count}");

                    foreach (var rel in allRels)
                    {
                        string typeName = "";
                        if (rel.RelationshipType == 1) typeName = "РОДИТЕЛЬ-РЕБЕНОК";
                        else if (rel.RelationshipType == 2) typeName = "СУПРУГ(А)";
                        else typeName = "ДРУГОЕ";

                        string direction = "";
                        if (rel.Person1Id == personId && rel.RelationshipType == 1) direction = " (как родитель -> ребенок)";
                        else if (rel.Person2Id == personId && rel.RelationshipType == 1) direction = " (как ребенок -> родитель)";

                        System.Diagnostics.Debug.WriteLine($"  Отношение {rel.Id}: {rel.Person1Id} -> {rel.Person2Id}, тип={rel.RelationshipType} ({typeName}){direction}");
                    }

                    // Отношения где персона - РОДИТЕЛЬ (Person1Id) - ЭТО ДЕТИ
                    var asParent = context.Relationships
                        .Where(r => r.Person1Id == personId && r.RelationshipType == 1)
                        .ToList();

                    System.Diagnostics.Debug.WriteLine($"\n--- ДЕТИ (персона как родитель, Person1Id={personId}) ---");
                    System.Diagnostics.Debug.WriteLine($"Найдено записей: {asParent.Count}");

                    if (asParent.Any())
                    {
                        foreach (var rel in asParent)
                        {
                            var child = context.Persons.FirstOrDefault(p => p.Id == rel.Person2Id);
                            if (child != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"  ✓ Ребенок ID={child.Id}: {child.LastName} {child.FirstName} {child.Patronymic}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"  ✗ РЕБЕНОК ID={rel.Person2Id} НЕ НАЙДЕН в таблице Persons!");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("  Записей о детях не найдено");
                    }

                    // Отношения где персона - РЕБЕНОК (Person2Id) - ЭТО РОДИТЕЛИ
                    var asChild = context.Relationships
                        .Where(r => r.Person2Id == personId && r.RelationshipType == 1)
                        .ToList();

                    System.Diagnostics.Debug.WriteLine($"\n--- РОДИТЕЛИ (персона как ребенок, Person2Id={personId}) ---");
                    System.Diagnostics.Debug.WriteLine($"Найдено записей: {asChild.Count}");

                    if (asChild.Any())
                    {
                        foreach (var rel in asChild)
                        {
                            var parent = context.Persons.FirstOrDefault(p => p.Id == rel.Person1Id);
                            if (parent != null)
                            {
                                string parentType = parent.GenderId == 1 ? "Отец" : (parent.GenderId == 2 ? "Мать" : "Родитель");
                                System.Diagnostics.Debug.WriteLine($"  {parentType} ID={parent.Id}: {parent.LastName} {parent.FirstName} {parent.Patronymic}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"  ✗ РОДИТЕЛЬ ID={rel.Person1Id} НЕ НАЙДЕН в таблице Persons!");
                            }
                        }
                    }

                    // Отношения СУПРУГ(И)
                    var spouses = context.Relationships
                        .Where(r => (r.Person1Id == personId || r.Person2Id == personId) && r.RelationshipType == 2)
                        .ToList();

                    System.Diagnostics.Debug.WriteLine($"\n--- СУПРУГ(И) ---");
                    System.Diagnostics.Debug.WriteLine($"Найдено записей: {spouses.Count}");

                    if (spouses.Any())
                    {
                        foreach (var rel in spouses)
                        {
                            int spouseId = rel.Person1Id == personId ? rel.Person2Id : rel.Person1Id;
                            var spouse = context.Persons.FirstOrDefault(p => p.Id == spouseId);
                            if (spouse != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"  Супруг(а) ID={spouse.Id}: {spouse.LastName} {spouse.FirstName} {spouse.Patronymic}");
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine("=================================");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка диагностики: {ex.Message}");
            }
        }

        private void LoadPersonData()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var person = context.Persons.FirstOrDefault(p => p.Id == personId);
                    if (person == null)
                    {
                        MessageBox.Show("Персона не найдена");
                        NavigationService.GoBack();
                        return;
                    }

                    // Формируем ФИО
                    string fullName = $"{person.LastName} {person.FirstName}";
                    if (!string.IsNullOrEmpty(person.Patronymic))
                        fullName += $" {person.Patronymic}";
                    txtFullName.Text = fullName;

                    // Даты
                    txtBirthDate.Text = person.BirthDate?.ToString("dd.MM.yyyy") ?? "?";
                    txtDeathDate.Text = person.DeathDate?.ToString("dd.MM.yyyy") ?? "...";

                    // Места
                    txtBirthPlace.Text = string.IsNullOrEmpty(person.BirthPlace)
                        ? "Место рождения: не указано"
                        : $"Место рождения: {person.BirthPlace}";

                    txtDeathPlace.Text = string.IsNullOrEmpty(person.DeathPlace)
                        ? "Место смерти: не указано"
                        : $"Место смерти: {person.DeathPlace}";

                    // Биография
                    txtBiography.Text = string.IsNullOrEmpty(person.Biography)
                        ? "Биография не добавлена"
                        : person.Biography;

                    // Пол
                    var gender = context.Genders.FirstOrDefault(g => g.Id == person.GenderId);
                    if (gender != null)
                    {
                        txtGender.Text = gender.Name;
                        txtGenderSymbol.Text = gender.Symbol ?? "👤";
                    }

                    // Фото профиля из папки Media
                    if (!string.IsNullOrEmpty(person.ProfilePhotoPath))
                    {
                        try
                        {
                            string fullPath = FileHelper.GetFullFilePath(person.ProfilePhotoPath);
                            if (File.Exists(fullPath))
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.UriSource = new Uri(fullPath);
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.EndInit();

                                imgProfile.Source = bitmap;
                                imgProfile.Visibility = Visibility.Visible;
                                txtNoProfilePhoto.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                imgProfile.Visibility = Visibility.Collapsed;
                                txtNoProfilePhoto.Visibility = Visibility.Visible;
                            }
                        }
                        catch
                        {
                            imgProfile.Visibility = Visibility.Collapsed;
                            txtNoProfilePhoto.Visibility = Visibility.Visible;
                        }
                    }

                    // Сбрасываем обработчики
                    txtFather.MouseLeftButtonUp -= TextBlock_MouseLeftButtonUp;
                    txtMother.MouseLeftButtonUp -= TextBlock_MouseLeftButtonUp;
                    txtSpouse.MouseLeftButtonUp -= TextBlock_MouseLeftButtonUp;
                    txtChildren.MouseLeftButtonUp -= TextBlock_MouseLeftButtonUp;

                    // ========== РОДИТЕЛИ (где персона - ребенок) ==========
                    var parentRelations = context.Relationships
                        .Where(r => r.Person2Id == personId && r.RelationshipType == 1)
                        .Select(r => r.Person1Id)
                        .ToList();

                    if (parentRelations.Any())
                    {
                        int? fatherId = null;
                        int? motherId = null;

                        foreach (var parentId in parentRelations)
                        {
                            var parent = context.Persons.FirstOrDefault(p => p.Id == parentId);
                            if (parent != null)
                            {
                                if (parent.GenderId == 1) // Мужской
                                    fatherId = parent.Id;
                                else if (parent.GenderId == 2) // Женский
                                    motherId = parent.Id;
                            }
                        }

                        // Если не удалось определить по полу, берем первого как отца, второго как мать
                        if (fatherId == null && parentRelations.Count >= 1)
                        {
                            var firstParent = context.Persons.FirstOrDefault(p => p.Id == parentRelations[0]);
                            if (firstParent != null)
                                fatherId = firstParent.Id;
                        }

                        if (motherId == null && parentRelations.Count >= 2)
                        {
                            var secondParent = context.Persons.FirstOrDefault(p => p.Id == parentRelations[1]);
                            if (secondParent != null)
                                motherId = secondParent.Id;
                        }

                        // Отображаем отца
                        if (fatherId.HasValue)
                        {
                            var father = context.Persons.FirstOrDefault(p => p.Id == fatherId);
                            if (father != null)
                            {
                                string fatherName = $"{father.LastName} {father.FirstName}";
                                if (!string.IsNullOrEmpty(father.Patronymic))
                                    fatherName += $" {father.Patronymic}";
                                txtFather.Text = fatherName;
                                txtFather.Tag = father.Id;
                                txtFather.Cursor = Cursors.Hand;
                                txtFather.MouseLeftButtonUp += TextBlock_MouseLeftButtonUp;
                            }
                            else
                                txtFather.Text = "Отец: не указан";
                        }
                        else
                            txtFather.Text = "Отец: не указан";

                        // Отображаем мать
                        if (motherId.HasValue)
                        {
                            var mother = context.Persons.FirstOrDefault(p => p.Id == motherId);
                            if (mother != null)
                            {
                                string motherName = $"{mother.LastName} {mother.FirstName}";
                                if (!string.IsNullOrEmpty(mother.Patronymic))
                                    motherName += $" {mother.Patronymic}";
                                txtMother.Text = motherName;
                                txtMother.Tag = mother.Id;
                                txtMother.Cursor = Cursors.Hand;
                                txtMother.MouseLeftButtonUp += TextBlock_MouseLeftButtonUp;
                            }
                            else
                                txtMother.Text = "Мать: не указана";
                        }
                        else
                            txtMother.Text = "Мать: не указана";
                    }
                    else
                    {
                        txtFather.Text = "Отец: не указан";
                        txtMother.Text = "Мать: не указана";
                    }

                    // ========== СУПРУГ(А) ==========
                    var spouseRel = context.Relationships
                        .FirstOrDefault(r => (r.Person1Id == personId || r.Person2Id == personId)
                                           && r.RelationshipType == 2);

                    if (spouseRel != null)
                    {
                        int spouseId = spouseRel.Person1Id == personId ? spouseRel.Person2Id : spouseRel.Person1Id;
                        var spouse = context.Persons.FirstOrDefault(p => p.Id == spouseId);
                        if (spouse != null)
                        {
                            string spouseName = $"{spouse.LastName} {spouse.FirstName}";
                            if (!string.IsNullOrEmpty(spouse.Patronymic))
                                spouseName += $" {spouse.Patronymic}";
                            txtSpouse.Text = spouseName;
                            txtSpouse.Tag = spouse.Id;
                            txtSpouse.Cursor = Cursors.Hand;
                            txtSpouse.MouseLeftButtonUp += TextBlock_MouseLeftButtonUp;
                        }
                        else
                            txtSpouse.Text = "нет";
                    }
                    else
                        txtSpouse.Text = "нет";

                    // ========== ДЕТИ (где персона - родитель) ==========
                    // ИСПРАВЛЕННЫЙ БЛОК С ДИАГНОСТИКОЙ
                    try
                    {
                        var childRelations = context.Relationships
                            .Where(r => r.Person1Id == personId && r.RelationshipType == 1)
                            .ToList();

                        System.Diagnostics.Debug.WriteLine($"Загрузка детей: найдено {childRelations.Count} записей в Relationships для Person1Id={personId}");

                        if (childRelations.Any())
                        {
                            var childIds = childRelations.Select(r => r.Person2Id).ToList();

                            var children = context.Persons
                                .Where(p => childIds.Contains(p.Id))
                                .ToList();

                            System.Diagnostics.Debug.WriteLine($"Найдено {children.Count} детей в таблице Persons");

                            if (children.Any())
                            {
                                var childNames = new List<string>();
                                var childIdList = new List<int>();

                                foreach (var child in children)
                                {
                                    string name = $"{child.LastName} {child.FirstName}";
                                    if (!string.IsNullOrEmpty(child.Patronymic))
                                        name += $" {child.Patronymic}";
                                    childNames.Add(name);
                                    childIdList.Add(child.Id);

                                    System.Diagnostics.Debug.WriteLine($"  - Ребенок: {name} (ID={child.Id})");
                                }

                                txtChildren.Text = string.Join(", ", childNames);
                                txtChildren.Tag = childIdList;
                                txtChildren.Cursor = Cursors.Hand;
                                txtChildren.MouseLeftButtonUp += TextBlock_MouseLeftButtonUp;
                            }
                            else
                            {
                                txtChildren.Text = "нет (записи в Relationships есть, но люди не найдены)";
                                System.Diagnostics.Debug.WriteLine("ВНИМАНИЕ: Записи в Relationships есть, но дети не найдены в Persons!");
                            }
                        }
                        else
                        {
                            txtChildren.Text = "нет";
                            System.Diagnostics.Debug.WriteLine("Записей о детях не найдено в Relationships");
                        }
                    }
                    catch (Exception ex)
                    {
                        txtChildren.Text = "ошибка";
                        System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке детей: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock?.Tag != null)
            {
                if (textBlock.Tag is int id)
                {
                    NavigateToPerson(id);
                }
                else if (textBlock.Tag is List<int> ids && ids.Any())
                {
                    NavigateToPerson(ids.First());
                }
            }
        }

        private void NavigateToPerson(int id)
        {
            NavigationService.Navigate(new PersonProfilePage(id));
        }

        private void LoadStories()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var storyList = context.Stories
                        .Where(s => s.PersonId == personId)
                        .OrderByDescending(s => s.EventDate ?? DateTime.MinValue)
                        .ToList();

                    stories = storyList.Select(s => new StoryItem
                    {
                        Id = s.Id,
                        Title = s.Title,
                        Content = s.Content,
                        EventDate = s.EventDate,
                        EventDateString = s.EventDate?.ToString("dd.MM.yyyy") ?? "Дата не указана",
                        ShortContent = s.Content.Length > 100
                            ? s.Content.Substring(0, 100) + "..."
                            : s.Content
                    }).ToList();

                    lvStories.ItemsSource = stories;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки историй: {ex.Message}");
            }
        }

        private void LoadMediaFiles()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var mediaLinks = context.MediaLinks
                        .Where(ml => ml.PersonId == personId)
                        .Select(ml => ml.MediaFileId)
                        .ToList();

                    var mediaFiles = context.MediaFiles
                        .Where(mf => mediaLinks.Contains(mf.Id))
                        .ToList();

                    var photos = new List<PhotoItem>();
                    var videos = new List<VideoItem>();
                    var audios = new List<AudioItem>();

                    foreach (var file in mediaFiles)
                    {
                        var mediaType = context.MediaTypes.FirstOrDefault(mt => mt.Id == file.MediaTypeId);
                        string typeName = mediaType?.Name ?? "";

                        string fullPath = FileHelper.GetFullFilePath(file.FilePath);
                        bool fileExists = File.Exists(fullPath);

                        if (typeName.Contains("Изображение") || typeName.Contains("Image") || typeName.Contains("Фото"))
                        {
                            photos.Add(new PhotoItem
                            {
                                Id = file.Id,
                                FilePath = fullPath,
                                FileName = file.FileName,
                                ThumbPath = fullPath
                            });
                        }
                        else if (typeName.Contains("Видео") || typeName.Contains("Video"))
                        {
                            videos.Add(new VideoItem
                            {
                                Id = file.Id,
                                FileName = file.FileName,
                                FilePath = fullPath
                            });
                        }
                        else if (typeName.Contains("Аудио") || typeName.Contains("Audio"))
                        {
                            audios.Add(new AudioItem
                            {
                                Id = file.Id,
                                FileName = file.FileName,
                                FilePath = fullPath
                            });
                        }
                    }

                    icPhotos.ItemsSource = photos;
                    icVideos.ItemsSource = videos;
                    icAudios.ItemsSource = audios;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки медиафайлов: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
            else
                NavigationService.Navigate(new MainPage());
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new EditPersonPage(personId));
        }

        private void AddStoryButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new EditStoryPage(personId));
        }

        private void AddPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new EditPersonPage(personId));
        }

        private void AddMediaButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция добавления медиафайлов к персоне в разработке", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ReadStory_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag != null)
            {
                int storyId = (int)btn.Tag;
                var story = stories.FirstOrDefault(s => s.Id == storyId);
                if (story != null)
                {
                    MessageBox.Show($"{story.Title}\n\n{story.Content}", "История",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void Photo_Click(object sender, MouseButtonEventArgs e)
        {
            var img = sender as Image;
            if (img?.Tag != null)
            {
                int photoId = (int)img.Tag;
                var photos = icPhotos.ItemsSource as List<PhotoItem>;
                var photo = photos?.FirstOrDefault(p => p.Id == photoId);
                if (photo != null && File.Exists(photo.FilePath))
                {
                    System.Diagnostics.Process.Start(photo.FilePath);
                }
            }
        }

        private void PlayAudio_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag != null)
            {
                int audioId = (int)btn.Tag;
                var audios = icAudios.ItemsSource as List<AudioItem>;
                var audio = audios?.FirstOrDefault(a => a.Id == audioId);
                if (audio != null && File.Exists(audio.FilePath))
                {
                    System.Diagnostics.Process.Start(audio.FilePath);
                }
            }
        }
    }
}

namespace Genealogy.Classes
{
    public static class FileHelper
    {
        private static string _mediaFolderPath = null;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Получает путь к папке Media в корне проекта
        /// </summary>
        public static string GetMediaFolderPath()
        {
            if (_mediaFolderPath == null)
            {
                lock (_lockObject)
                {
                    if (_mediaFolderPath == null)
                    {
                        try
                        {
                            // Получаем путь к исполняемому файлу (bin/Debug или bin/Release)
                            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                            // Поднимаемся на уровень выше от bin/Debug до корня проекта
                            // Для отладки: ...\bin\Debug\ -> ...\bin\ -> ...\ -> корень проекта
                            string projectDirectory = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\..\"));

                            // Создаем путь к папке Media в корне проекта
                            _mediaFolderPath = Path.Combine(projectDirectory, "Media");

                            // Создаем папку, если её нет
                            if (!Directory.Exists(_mediaFolderPath))
                            {
                                Directory.CreateDirectory(_mediaFolderPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Если не удалось создать в корне проекта, используем папку рядом с exe
                            _mediaFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media");

                            if (!Directory.Exists(_mediaFolderPath))
                            {
                                Directory.CreateDirectory(_mediaFolderPath);
                            }

                            System.Diagnostics.Debug.WriteLine($"Ошибка при создании папки Media: {ex.Message}. Используем: {_mediaFolderPath}");
                        }
                    }
                }
            }
            return _mediaFolderPath;
        }

        /// <summary>
        /// Получает полный путь к файлу
        /// </summary>
        /// <param name="relativePath">Относительный путь или имя файла</param>
        /// <returns>Полный путь к файлу или null, если путь невалидный</returns>
        public static string GetFullFilePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            try
            {
                // Если путь уже абсолютный и файл существует
                if (Path.IsPathRooted(relativePath) && File.Exists(relativePath))
                    return relativePath;

                // Получаем имя файла из пути (отсекаем возможные папки)
                string fileName = Path.GetFileName(relativePath);

                // Если после получения имени файла осталась пустая строка, значит путь был некорректным
                if (string.IsNullOrEmpty(fileName))
                    return null;

                // Ищем файл в папке Media
                string mediaFolder = GetMediaFolderPath();
                string fullPath = Path.Combine(mediaFolder, fileName);

                // Проверяем существование файла
                if (File.Exists(fullPath))
                    return fullPath;

                // Если файл не найден, пробуем другие возможные расположения

                // 1. Пробуем найти в подпапках Media
                string[] searchPaths = {
                    Path.Combine(mediaFolder, "Images"),
                    Path.Combine(mediaFolder, "Photos"),
                    Path.Combine(mediaFolder, "Videos"),
                    Path.Combine(mediaFolder, "Audio"),
                    Path.Combine(mediaFolder, "Documents")
                };

                foreach (var searchPath in searchPaths)
                {
                    if (Directory.Exists(searchPath))
                    {
                        string testPath = Path.Combine(searchPath, fileName);
                        if (File.Exists(testPath))
                            return testPath;
                    }
                }

                // 2. Пробуем найти в папке с исполняемым файлом
                string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                if (File.Exists(exePath))
                    return exePath;

                // 3. Пробуем найти по исходному пути (если это был полный путь, но файл не найден)
                if (File.Exists(relativePath))
                    return relativePath;

                // Файл не найден нигде
                return fullPath; // Возвращаем ожидаемый путь, даже если файла нет
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в GetFullFilePath для '{relativePath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Проверяет, существует ли файл
        /// </summary>
        public static bool FileExists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string fullPath = GetFullFilePath(filePath);
            return !string.IsNullOrEmpty(fullPath) && File.Exists(fullPath);
        }

        /// <summary>
        /// Сохраняет файл в папку Media
        /// </summary>
        /// <param name="sourceFilePath">Исходный путь к файлу</param>
        /// <param name="targetFileName">Имя файла для сохранения (если null, используется исходное имя)</param>
        /// <returns>Относительный путь к сохраненному файлу или null при ошибке</returns>
        public static string SaveFileToMedia(string sourceFilePath, string targetFileName = null)
        {
            try
            {
                if (!File.Exists(sourceFilePath))
                    return null;

                string mediaFolder = GetMediaFolderPath();

                // Определяем имя файла
                string fileName = targetFileName;
                if (string.IsNullOrEmpty(fileName))
                    fileName = Path.GetFileName(sourceFilePath);

                // Генерируем уникальное имя, если файл уже существует
                string fullPath = Path.Combine(mediaFolder, fileName);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);
                int counter = 1;

                while (File.Exists(fullPath))
                {
                    fileName = $"{fileNameWithoutExt}_{counter}{extension}";
                    fullPath = Path.Combine(mediaFolder, fileName);
                    counter++;
                }

                // Копируем файл
                File.Copy(sourceFilePath, fullPath);

                // Возвращаем относительный путь (только имя файла)
                return fileName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при сохранении файла: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Удаляет файл из папки Media
        /// </summary>
        public static bool DeleteFile(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                    return false;

                string mediaFolder = GetMediaFolderPath();
                string fullPath = Path.Combine(mediaFolder, fileName);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при удалении файла: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получает размер файла в байтах
        /// </summary>
        public static long GetFileSize(string filePath)
        {
            try
            {
                string fullPath = GetFullFilePath(filePath);
                if (File.Exists(fullPath))
                {
                    return new FileInfo(fullPath).Length;
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Проверяет, является ли файл изображением по расширению
        /// </summary>
        public static bool IsImageFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" ||
                   ext == ".gif" || ext == ".bmp" || ext == ".tiff" ||
                   ext == ".ico" || ext == ".webp";
        }

        /// <summary>
        /// Проверяет, является ли файл видео по расширению
        /// </summary>
        public static bool IsVideoFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".mp4" || ext == ".avi" || ext == ".mov" ||
                   ext == ".wmv" || ext == ".flv" || ext == ".mkv" ||
                   ext == ".webm" || ext == ".m4v";
        }

        /// <summary>
        /// Проверяет, является ли файл аудио по расширению
        /// </summary>
        public static bool IsAudioFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".mp3" || ext == ".wav" || ext == ".flac" ||
                   ext == ".aac" || ext == ".ogg" || ext == ".m4a" ||
                   ext == ".wma";
        }

        /// <summary>
        /// Получает тип медиа по расширению файла
        /// </summary>
        public static string GetMediaTypeByExtension(string fileName)
        {
            if (IsImageFile(fileName))
                return "Изображение";
            if (IsVideoFile(fileName))
                return "Видео";
            if (IsAudioFile(fileName))
                return "Аудио";
            return "Документ";
        }
    }
}