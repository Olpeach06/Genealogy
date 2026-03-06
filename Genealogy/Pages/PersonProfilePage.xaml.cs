using Genealogy.Classes;
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

namespace Genealogy.Pages
{
    public partial class PersonProfilePage : Page
    {
        private int personId;
        private List<StoryItem> stories = new List<StoryItem>();

        // Класс для истории
        public class StoryItem
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public DateTime? EventDate { get; set; }
            public string EventDateString { get; set; }
            public string ShortContent { get; set; }
        }

        // Класс для фото
        public class PhotoItem
        {
            public int Id { get; set; }
            public string FilePath { get; set; }
            public string ThumbPath { get; set; }
        }

        // Класс для видео
        public class VideoItem
        {
            public int Id { get; set; }
            public string FileName { get; set; }
            public string FilePath { get; set; }
        }

        // Класс для аудио
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
            LoadPersonData();
            LoadStories();
            LoadMediaFiles();

            // Проверка прав на редактирование
            bool canEdit = Session.IsAdmin || Session.IsEditor;
            btnEdit.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddStory.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddStoryBottom.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddPhoto.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnAddMedia.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
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

                    // Основная информация
                    txtFullName.Text = $"{person.LastName} {person.FirstName} {person.Patronymic}".Trim();
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

                    // Пол
                    var gender = context.Genders.FirstOrDefault(g => g.Id == person.GenderId);
                    if (gender != null)
                    {
                        txtGender.Text = gender.Name;
                        txtGenderSymbol.Text = gender.Symbol ?? "👤";
                    }

                    // Фото профиля
                    if (!string.IsNullOrEmpty(person.ProfilePhotoPath))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(person.ProfilePhotoPath, UriKind.RelativeOrAbsolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();

                            imgProfile.Source = bitmap;
                            imgProfile.Visibility = Visibility.Visible;
                            txtNoProfilePhoto.Visibility = Visibility.Collapsed;
                        }
                        catch
                        {
                            // Если фото не загружается, оставляем заглушку
                        }
                    }

                    // Родители
                    var parentRelations = context.Relationships
                        .Where(r => r.Person2Id == personId && r.RelationshipType == 1)
                        .Select(r => r.Person1Id)
                        .ToList();

                    if (parentRelations.Count >= 1)
                    {
                        var father = context.Persons.FirstOrDefault(p => p.Id == parentRelations[0]);
                        if (father != null)
                            txtFather.Text = $"Отец: {father.FirstName} {father.LastName}";
                    }

                    if (parentRelations.Count >= 2)
                    {
                        var mother = context.Persons.FirstOrDefault(p => p.Id == parentRelations[1]);
                        if (mother != null)
                            txtMother.Text = $"Мать: {mother.FirstName} {mother.LastName}";
                    }

                    // Супруг(а)
                    var spouseRel = context.Relationships
                        .FirstOrDefault(r => (r.Person1Id == personId || r.Person2Id == personId)
                                           && r.RelationshipType == 2);

                    if (spouseRel != null)
                    {
                        int spouseId = spouseRel.Person1Id == personId ? spouseRel.Person2Id : spouseRel.Person1Id;
                        var spouse = context.Persons.FirstOrDefault(p => p.Id == spouseId);
                        if (spouse != null)
                            txtSpouse.Text = $"{spouse.FirstName} {spouse.LastName}";
                    }

                    // Дети
                    var childIds = context.Relationships
                        .Where(r => r.Person1Id == personId && r.RelationshipType == 1)
                        .Select(r => r.Person2Id)
                        .ToList();

                    if (childIds.Any())
                    {
                        var children = context.Persons
                            .Where(p => childIds.Contains(p.Id))
                            .Select(p => $"{p.FirstName} {p.LastName}")
                            .ToList();
                        txtChildren.Text = string.Join(", ", children);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
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
                    // Получаем все медиафайлы, связанные с этой персоной
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

                        if (typeName.Contains("Изображение"))
                        {
                            photos.Add(new PhotoItem
                            {
                                Id = file.Id,
                                FilePath = file.FilePath,
                                ThumbPath = file.FilePath // В реальном проекте нужно создавать превью
                            });
                        }
                        else if (typeName.Contains("Видео"))
                        {
                            videos.Add(new VideoItem
                            {
                                Id = file.Id,
                                FileName = file.FileName,
                                FilePath = file.FilePath
                            });
                        }
                        else if (typeName.Contains("Аудио"))
                        {
                            audios.Add(new AudioItem
                            {
                                Id = file.Id,
                                FileName = file.FileName,
                                FilePath = file.FilePath
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
            NavigationService.GoBack();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new EditPersonPage(personId));
        }

        private void AddStoryButton_Click(object sender, RoutedEventArgs e)
        {
            // Пока просто заглушка
            MessageBox.Show("Добавление истории для ID: " + personId);
        }

        private void AddPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            // Пока просто заглушка
            MessageBox.Show("Добавление фото для ID: " + personId);
        }

        private void AddMediaButton_Click(object sender, RoutedEventArgs e)
        {
            // Пока просто заглушка
            MessageBox.Show("Добавление медиафайла для ID: " + personId);
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
                // Пока просто заглушка
                MessageBox.Show("Просмотр фото ID: " + img.Tag);
            }
        }

        private void PlayAudio_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag != null)
            {
                // Пока просто заглушка
                MessageBox.Show("Воспроизведение аудио ID: " + btn.Tag);
            }
        }
    }
}
