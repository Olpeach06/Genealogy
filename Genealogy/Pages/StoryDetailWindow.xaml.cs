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
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using Genealogy.AppData;

namespace Genealogy.Pages
{
    public partial class StoryDetailWindow : Window
    {
        private int storyId;
        private int personId;

        public class MediaItem
        {
            public int Id { get; set; }
            public string FileName { get; set; }
            public string FilePath { get; set; }
            public string Icon { get; set; }
            public int MediaTypeId { get; set; }
        }

        public StoryDetailWindow(int storyId, int personId, string personName)
        {
            InitializeComponent();
            this.storyId = storyId;
            this.personId = personId;
            txtPersonInfo.Text = $"Персона: {personName}";

            Loaded += StoryDetailWindow_Loaded;
        }

        private void StoryDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStory();
        }

        private void LoadStory()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var story = context.Stories.FirstOrDefault(s => s.Id == storyId);
                    if (story == null)
                    {
                        MessageBox.Show("История не найдена!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        Close();
                        return;
                    }

                    txtTitle.Text = story.Title;

                    // Форматируем дату события
                    if (story.EventDate.HasValue)
                    {
                        txtEventDate.Text = $"Дата события: {story.EventDate.Value:dd.MM.yyyy}";
                    }
                    else if (!string.IsNullOrEmpty(story.EventDateText))
                    {
                        txtEventDate.Text = $"Дата события: {story.EventDateText}";
                    }
                    else
                    {
                        txtEventDate.Text = "Дата события: не указана";
                    }

                    txtContent.Text = story.Content;

                    // Загружаем медиафайлы
                    var mediaLinks = context.MediaLinks
                        .Where(ml => ml.StoryId == storyId)
                        .Select(ml => ml.MediaFileId)
                        .ToList();

                    var mediaFiles = context.MediaFiles
                        .Where(mf => mediaLinks.Contains(mf.Id))
                        .ToList();

                    var mediaItems = new List<MediaItem>();
                    foreach (var media in mediaFiles)
                    {
                        string icon = "";
                        if (media.MediaTypeId == 1) icon = "📷";
                        else if (media.MediaTypeId == 2) icon = "🎥";
                        else if (media.MediaTypeId == 3) icon = "🎵";

                        mediaItems.Add(new MediaItem
                        {
                            Id = media.Id,
                            FileName = media.FileName,
                            FilePath = media.FilePath,
                            Icon = icon,
                            MediaTypeId = media.MediaTypeId
                        });
                    }

                    icMedia.ItemsSource = mediaItems;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки истории: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Media_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.DataContext is MediaItem media)
            {
                try
                {
                    // Открываем файл в стандартной программе
                    if (File.Exists(media.FilePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = media.FilePath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show("Файл не найден на диске!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}