using Genealogy.AppData;
using Genealogy.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Genealogy.AppData;
using Genealogy.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

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
            bool isAdmin = Session.IsAdmin;

            btnEdit.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddStory.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddStoryBottom.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddPhoto.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddMedia.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;

            // Кнопки "Удалить все" видны только администраторам
            btnDeleteAllStories.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnDeleteAllMedia.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            // Запускаем диагностику (можно закомментировать после отладки)
            DebugCheckDatabase();

            // После загрузки всех данных добавляем обработчики контекстного меню
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AddContextMenuToStories();
                AddContextMenuToMedia(icPhotos, "photoBorder");
                AddContextMenuToMedia(icVideos, "videoBorder");
                AddContextMenuToMedia(icAudios, "audioBorder");
            }));
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

                    var person = context.Persons.FirstOrDefault(p => p.Id == personId);
                    if (person != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Текущая персона: ID={person.Id}, {person.LastName} {person.FirstName} {person.Patronymic}");
                        System.Diagnostics.Debug.WriteLine($"  Пол: {person.GenderId}");
                    }

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

                    string fullName = $"{person.LastName} {person.FirstName}";
                    if (!string.IsNullOrEmpty(person.Patronymic))
                        fullName += $" {person.Patronymic}";
                    txtFullName.Text = fullName;

                    txtBirthDate.Text = person.BirthDate?.ToString("dd.MM.yyyy") ?? "?";
                    txtDeathDate.Text = person.DeathDate?.ToString("dd.MM.yyyy") ?? "...";

                    txtBirthPlace.Text = string.IsNullOrEmpty(person.BirthPlace)
                        ? "Место рождения: не указано"
                        : $"Место рождения: {person.BirthPlace}";

                    txtDeathPlace.Text = string.IsNullOrEmpty(person.DeathPlace)
                        ? "Место смерти: не указано"
                        : $"Место смерти: {person.DeathPlace}";

                    txtBiography.Text = string.IsNullOrEmpty(person.Biography)
                        ? "Биография не добавлена"
                        : person.Biography;

                    var gender = context.Genders.FirstOrDefault(g => g.Id == person.GenderId);
                    if (gender != null)
                    {
                        txtGender.Text = gender.Name;
                        txtGenderSymbol.Text = gender.Symbol ?? "👤";
                    }

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
                                if (parent.GenderId == 1)
                                    fatherId = parent.Id;
                                else if (parent.GenderId == 2)
                                    motherId = parent.Id;
                            }
                        }

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
                        EventDateString = s.EventDate?.ToString("dd.MM.yyyy") ?? s.EventDateText ?? "Дата не указана",
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
                    var stories = context.Stories
                        .Where(s => s.PersonId == personId)
                        .Select(s => s.Id)
                        .ToList();

                    var mediaLinks = context.MediaLinks
                        .Where(ml => ml.StoryId.HasValue && stories.Contains(ml.StoryId.Value))
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

                    UpdateTabHeaders(photos.Count, videos.Count, audios.Count);

                    System.Diagnostics.Debug.WriteLine($"Загружено медиафайлов для персоны {personId}:");
                    System.Diagnostics.Debug.WriteLine($"  - Фото: {photos.Count}");
                    System.Diagnostics.Debug.WriteLine($"  - Видео: {videos.Count}");
                    System.Diagnostics.Debug.WriteLine($"  - Аудио: {audios.Count}");
                    System.Diagnostics.Debug.WriteLine($"  - Всего историй: {stories.Count}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки медиафайлов: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки медиафайлов: {ex.Message}");
            }
        }

        private void UpdateTabHeaders(int photoCount, int videoCount, int audioCount)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var tabControl = FindVisualChild<TabControl>(this);
                if (tabControl != null && tabControl.Items.Count >= 3)
                {
                    var photoTab = tabControl.Items[0] as TabItem;
                    var videoTab = tabControl.Items[1] as TabItem;
                    var audioTab = tabControl.Items[2] as TabItem;

                    if (photoTab != null)
                        photoTab.Header = $"📷 Фотографии ({photoCount})";
                    if (videoTab != null)
                        videoTab.Header = $"🎥 Видео ({videoCount})";
                    if (audioTab != null)
                        audioTab.Header = $"🎵 Аудио ({audioCount})";
                }
            }));
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    var descendant = FindVisualChild<T>(child);
                    if (descendant != null)
                        return descendant;
                }
            }
            return null;
        }

        // ==================== КОНТЕКСТНОЕ МЕНЮ ДЛЯ ИСТОРИЙ ====================

        private void AddContextMenuToStories()
        {
            if (lvStories?.ItemsSource == null) return;

            var containerGenerator = lvStories.ItemContainerGenerator;
            for (int i = 0; i < lvStories.Items.Count; i++)
            {
                var item = lvStories.Items[i];
                var container = containerGenerator.ContainerFromItem(item) as ListViewItem;
                if (container != null)
                {
                    var border = FindVisualChildByName<Border>(container, "storyBorder");
                    if (border != null)
                    {
                        border.Tag = ((StoryItem)item).Id;
                        border.MouseRightButtonDown += Story_MouseRightButtonDown;
                    }
                }
            }
        }

        private void Story_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag == null) return;

            int storyId = (int)border.Tag;

            var contextMenu = new ContextMenu();

            if (Session.IsAdmin || Session.IsEditor)
            {
                var editItem = new MenuItem { Header = "✎ Редактировать историю" };
                editItem.Click += (s, args) => EditStory(storyId);
                contextMenu.Items.Add(editItem);
            }

            if (Session.IsAdmin)
            {
                var deleteItem = new MenuItem { Header = "🗑 Удалить историю" };
                deleteItem.Click += (s, args) => DeleteStoryById(storyId);
                contextMenu.Items.Add(deleteItem);
            }

            if (contextMenu.Items.Count > 0)
            {
                border.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;
            }

            e.Handled = true;
        }

        private void EditStory(int storyId)
        {
            NavigationService.Navigate(new EditStoryPage(personId, storyId));
        }

        private async void DeleteStoryById(int storyId)
        {
            var result = MessageBox.Show("Вы уверены, что хотите удалить эту историю?\nВсе прикрепленные к ней медиафайлы также будут удалены!",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new GenealogyDBEntities())
                    {
                        var story = context.Stories.FirstOrDefault(s => s.Id == storyId);
                        if (story == null)
                        {
                            MessageBox.Show("История не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        var mediaLinks = context.MediaLinks.Where(ml => ml.StoryId == storyId).ToList();
                        var mediaFileIds = mediaLinks.Select(ml => ml.MediaFileId).ToList();

                        context.MediaLinks.RemoveRange(mediaLinks);

                        var mediaFiles = context.MediaFiles.Where(mf => mediaFileIds.Contains(mf.Id)).ToList();

                        foreach (var mediaFile in mediaFiles)
                        {
                            FileHelper.DeleteFile(mediaFile.FilePath);
                        }

                        context.MediaFiles.RemoveRange(mediaFiles);
                        context.Stories.Remove(story);

                        await context.SaveChangesAsync();

                        MessageBox.Show("История успешно удалена!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        LoadStories();
                        LoadMediaFiles();
                        Dispatcher.BeginInvoke(new Action(() => AddContextMenuToStories()));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении истории: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ==================== КОНТЕКСТНОЕ МЕНЮ ДЛЯ МЕДИАФАЙЛОВ ====================

        private void AddContextMenuToMedia(ItemsControl itemsControl, string borderName)
        {
            if (itemsControl?.ItemsSource == null) return;

            var containerGenerator = itemsControl.ItemContainerGenerator;
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var item = itemsControl.Items[i];
                var container = containerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container != null)
                {
                    var border = FindVisualChildByName<Border>(container, borderName);
                    if (border != null)
                    {
                        int mediaId = 0;
                        if (item is PhotoItem photo) mediaId = photo.Id;
                        else if (item is VideoItem video) mediaId = video.Id;
                        else if (item is AudioItem audio) mediaId = audio.Id;

                        border.Tag = mediaId;
                        border.MouseRightButtonDown += Media_MouseRightButtonDown;
                    }
                }
            }
        }

        private void Media_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag == null) return;

            int mediaId = (int)border.Tag;

            var contextMenu = new ContextMenu();

            if (Session.IsAdmin)
            {
                var deleteItem = new MenuItem { Header = "🗑 Удалить" };
                deleteItem.Click += (s, args) => DeleteMediaFileById(mediaId);
                contextMenu.Items.Add(deleteItem);
            }

            if (contextMenu.Items.Count > 0)
            {
                border.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;
            }

            e.Handled = true;
        }

        private async void DeleteMediaFileById(int mediaFileId)
        {
            var result = MessageBox.Show("Вы уверены, что хотите удалить этот медиафайл?",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await DeleteMediaFile(mediaFileId, "медиафайл");
            }
        }

        // ==================== УДАЛЕНИЕ ВСЕХ ====================

        private async void DeleteAllStories_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите удалить ВСЕ истории этой персоны?\nВсе прикрепленные медиафайлы также будут удалены!",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new GenealogyDBEntities())
                    {
                        var stories = context.Stories.Where(s => s.PersonId == personId).ToList();
                        var storyIds = stories.Select(s => s.Id).ToList();

                        var mediaLinks = context.MediaLinks
                            .Where(ml => ml.StoryId.HasValue && storyIds.Contains(ml.StoryId.Value))
                            .ToList();

                        var mediaFileIds = mediaLinks.Select(ml => ml.MediaFileId).ToList();
                        var mediaFiles = context.MediaFiles.Where(mf => mediaFileIds.Contains(mf.Id)).ToList();

                        context.MediaLinks.RemoveRange(mediaLinks);

                        foreach (var file in mediaFiles)
                        {
                            FileHelper.DeleteFile(file.FilePath);
                        }

                        context.MediaFiles.RemoveRange(mediaFiles);
                        context.Stories.RemoveRange(stories);

                        await context.SaveChangesAsync();

                        MessageBox.Show("Все истории успешно удалены!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        LoadStories();
                        LoadMediaFiles();
                        Dispatcher.BeginInvoke(new Action(() => AddContextMenuToStories()));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении всех историй: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DeleteAllMedia_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите удалить ВСЕ медиафайлы, связанные с этой персоной?",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new GenealogyDBEntities())
                    {
                        var stories = context.Stories.Where(s => s.PersonId == personId).Select(s => s.Id).ToList();

                        var mediaLinks = context.MediaLinks
                            .Where(ml => ml.StoryId.HasValue && stories.Contains(ml.StoryId.Value))
                            .ToList();

                        var mediaFileIds = mediaLinks.Select(ml => ml.MediaFileId).ToList();
                        var mediaFiles = context.MediaFiles.Where(mf => mediaFileIds.Contains(mf.Id)).ToList();

                        foreach (var file in mediaFiles)
                        {
                            FileHelper.DeleteFile(file.FilePath);
                        }

                        context.MediaLinks.RemoveRange(mediaLinks);
                        context.MediaFiles.RemoveRange(mediaFiles);

                        await context.SaveChangesAsync();

                        MessageBox.Show("Все медиафайлы успешно удалены!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        LoadMediaFiles();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении всех медиафайлов: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ==================== УДАЛЕНИЕ МЕДИАФАЙЛА ====================

        private async Task DeleteMediaFile(int mediaFileId, string typeName)
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var mediaFile = context.MediaFiles.FirstOrDefault(mf => mf.Id == mediaFileId);
                    if (mediaFile == null)
                    {
                        MessageBox.Show("Файл не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var mediaLinks = context.MediaLinks.Where(ml => ml.MediaFileId == mediaFileId).ToList();
                    context.MediaLinks.RemoveRange(mediaLinks);

                    FileHelper.DeleteFile(mediaFile.FilePath);
                    context.MediaFiles.Remove(mediaFile);

                    await context.SaveChangesAsync();

                    MessageBox.Show($"{typeName} успешно удалено!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    LoadMediaFiles();
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AddContextMenuToMedia(icPhotos, "photoBorder");
                        AddContextMenuToMedia(icVideos, "videoBorder");
                        AddContextMenuToMedia(icAudios, "audioBorder");
                    }));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении {typeName}: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== ВСПОМОГАТЕЛЬНЫЙ МЕТОД ====================

        private T FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                    return element;

                var result = FindVisualChildByName<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        // ==================== ОБРАБОТЧИКИ КНОПОК ====================

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
            NavigationService.Navigate(new EditStoryPage(personId));
        }

        private void ReadStory_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            int storyId = (int)button.Tag;

            using (var context = new GenealogyDBEntities())
            {
                var story = context.Stories.FirstOrDefault(s => s.Id == storyId);
                if (story != null)
                {
                    var person = context.Persons.FirstOrDefault(p => p.Id == story.PersonId);
                    string personName = person != null ? $"{person.LastName} {person.FirstName}" : "Неизвестно";

                    var storyWindow = new StoryDetailWindow(storyId, story.PersonId, personName)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    storyWindow.ShowDialog();
                }
            }
        }

        private void Photo_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag != null)
            {
                int photoId = (int)border.Tag;
                var photos = icPhotos.ItemsSource as List<PhotoItem>;
                var photo = photos?.FirstOrDefault(p => p.Id == photoId);
                if (photo != null && File.Exists(photo.FilePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = photo.FilePath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Не удалось открыть фото: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Файл не найден на диске!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void Video_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag != null)
            {
                int videoId = (int)border.Tag;
                var videos = icVideos.ItemsSource as List<VideoItem>;
                var video = videos?.FirstOrDefault(v => v.Id == videoId);
                if (video != null && File.Exists(video.FilePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = video.FilePath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Не удалось открыть видео: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Видеофайл не найден на диске!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void PlayAudio_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag != null)
            {
                int audioId = (int)border.Tag;
                var audios = icAudios.ItemsSource as List<AudioItem>;
                var audio = audios?.FirstOrDefault(a => a.Id == audioId);
                if (audio != null && File.Exists(audio.FilePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = audio.FilePath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Не удалось открыть аудио: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Аудиофайл не найден на диске!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
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