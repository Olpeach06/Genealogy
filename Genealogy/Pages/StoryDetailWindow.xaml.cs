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
            public string FullPath { get; set; }
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

                        string fullPath = FindFile(media.FilePath, media.FileName);

                        mediaItems.Add(new MediaItem
                        {
                            Id = media.Id,
                            FileName = media.FileName,
                            FilePath = media.FilePath,
                            FullPath = fullPath,
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

        private string FindFile(string storedPath, string fileName)
        {
            // Получаем имя файла из сохраненного пути
            string fileNameOnly = System.IO.Path.GetFileName(storedPath);
            if (string.IsNullOrEmpty(fileNameOnly))
            {
                fileNameOnly = fileName;
            }

            // Список возможных путей для поиска
            List<string> possiblePaths = new List<string>();

            // 1. Текущая директория приложения
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            possiblePaths.Add(System.IO.Path.Combine(currentDir, fileNameOnly));
            possiblePaths.Add(System.IO.Path.Combine(currentDir, "Media", fileNameOnly));
            possiblePaths.Add(System.IO.Path.Combine(currentDir, "Media", fileName));
            possiblePaths.Add(System.IO.Path.Combine(currentDir, "..", "Media", fileNameOnly));
            possiblePaths.Add(System.IO.Path.Combine(currentDir, "..", "..", "Media", fileNameOnly));

            // 2. Директория проекта (на уровень выше)
            string projectDir = System.IO.Path.GetDirectoryName(currentDir);
            if (!string.IsNullOrEmpty(projectDir))
            {
                possiblePaths.Add(System.IO.Path.Combine(projectDir, "Media", fileNameOnly));
                possiblePaths.Add(System.IO.Path.Combine(projectDir, "Media", fileName));
                possiblePaths.Add(System.IO.Path.Combine(projectDir, "..", "Media", fileNameOnly));
            }

            // 3. Директория решения (на два уровня выше)
            string solutionDir = System.IO.Path.GetDirectoryName(projectDir);
            if (!string.IsNullOrEmpty(solutionDir))
            {
                possiblePaths.Add(System.IO.Path.Combine(solutionDir, "Media", fileNameOnly));
                possiblePaths.Add(System.IO.Path.Combine(solutionDir, "Media", fileName));
            }

            // 4. Папка Media в корне проекта (поиск вверх по дереву)
            string rootDir = currentDir;
            for (int i = 0; i < 5; i++) // Поднимаемся на 5 уровней вверх
            {
                string mediaPath = System.IO.Path.Combine(rootDir, "Media", fileNameOnly);
                if (!possiblePaths.Contains(mediaPath))
                    possiblePaths.Add(mediaPath);

                string mediaPathWithName = System.IO.Path.Combine(rootDir, "Media", fileName);
                if (!possiblePaths.Contains(mediaPathWithName))
                    possiblePaths.Add(mediaPathWithName);

                rootDir = System.IO.Path.GetDirectoryName(rootDir);
                if (string.IsNullOrEmpty(rootDir)) break;
            }

            // 5. Пути с использованием подстановки ..\Media
            string relativePath1 = System.IO.Path.Combine("..", "Media", fileNameOnly);
            string relativePath2 = System.IO.Path.Combine("..", "..", "Media", fileNameOnly);
            string relativePath3 = System.IO.Path.Combine("..", "..", "..", "Media", fileNameOnly);

            possiblePaths.Add(relativePath1);
            possiblePaths.Add(relativePath2);
            possiblePaths.Add(relativePath3);

            // 6. Поиск по всем подпапкам в текущей директории
            try
            {
                var foundFiles = System.IO.Directory.GetFiles(currentDir, fileNameOnly, System.IO.SearchOption.AllDirectories);
                foreach (var file in foundFiles)
                {
                    if (!possiblePaths.Contains(file))
                        possiblePaths.Add(file);
                }
            }
            catch { }

            // Проверяем все возможные пути и возвращаем первый существующий
            foreach (string path in possiblePaths.Distinct())
            {
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        // Нормализуем путь (преобразуем относительный в абсолютный)
                        string normalizedPath = System.IO.Path.GetFullPath(path);
                        if (System.IO.File.Exists(normalizedPath))
                        {
                            return normalizedPath;
                        }
                    }
                    catch { }
                }
            }

            // Если ничего не нашли, возвращаем исходный путь
            return storedPath;
        }

        private void Media_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.DataContext is MediaItem media)
            {
                try
                {
                    string filePath = media.FullPath;

                    // Если путь пустой или файл не существует, пытаемся найти заново
                    if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                    {
                        filePath = FindFile(media.FilePath, media.FileName);
                    }

                    if (System.IO.File.Exists(filePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show($"Файл \"{media.FileName}\" не найден!\n\n" +
                            $"Убедитесь, что файл находится в папке \"Media\" в корне проекта.\n" +
                            $"Ожидаемый путь: ...\\Genealogy\\Media\\{System.IO.Path.GetFileName(media.FilePath)}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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