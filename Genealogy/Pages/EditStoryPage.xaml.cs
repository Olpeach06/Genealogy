using Genealogy.AppData;
using Genealogy.Classes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace Genealogy.Pages
{
    public partial class EditStoryPage : Page
    {
        private int personId;
        private int? storyId = null;
        private List<AttachedMedia> attachedMedia = new List<AttachedMedia>();
        private bool _isProcessing = false; // Флаг для предотвращения двойных вызовов

        // Константы для ограничений
        private const int MAX_TITLE_LENGTH = 200;
        private const int MAX_CONTENT_LENGTH = 5000;
        private const int MAX_DATE_TEXT_LENGTH = 100;

        public class AttachedMedia
        {
            public int FileId { get; set; }
            public string FileName { get; set; }
            public string FilePath { get; set; }
            public int MediaTypeId { get; set; }
            public string Icon { get; set; }
            public bool IsNew { get; set; } = true;
        }

        public EditStoryPage(int personId)
        {
            InitializeComponent();
            this.personId = personId;
            this.storyId = null;
            txtPageTitle.Text = "ДОБАВЛЕНИЕ ИСТОРИИ";
            LoadPersonInfo();
            SetupDateRestrictions();
        }

        public EditStoryPage(int personId, int storyId) : this(personId)
        {
            this.storyId = storyId;
            txtPageTitle.Text = "РЕДАКТИРОВАНИЕ ИСТОРИИ";
            LoadStoryData();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ValidateFields(null, null);
        }

        private void SetupDateRestrictions()
        {
            // Ограничение даты - не позже сегодняшнего дня
            dpEventDate.DisplayDateEnd = DateTime.Today;

            // Можно также ограничить самую раннюю дату (например, 1700 год)
            dpEventDate.DisplayDateStart = new DateTime(1700, 1, 1);
        }

        // ==================== ОБРАБОТЧИКИ ENTER ====================

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Для многострочного текстового поля Enter должен работать как перевод строки
                if (sender == txtContent && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    // Shift+Enter - перевод строки (оставляем стандартное поведение)
                    return;
                }
                else if (sender == txtContent)
                {
                    // Просто Enter в многострочном поле - не сохраняем, а переводим строку
                    // Чтобы сохранить, нужно нажать Ctrl+Enter или использовать кнопку
                    return;
                }

                e.Handled = true;
                PerformSave();
            }
        }

        private void DatePicker_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                PerformSave();
            }
        }

        // ==================== ЗАГРУЗКА ДАННЫХ ====================

        private void LoadPersonInfo()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var person = context.Persons.FirstOrDefault(p => p.Id == personId);
                    if (person != null)
                    {
                        txtPersonInfo.Text = $"История для: {person.FirstName} {person.LastName}";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки информации о персоне: {ex.Message}");
            }
        }

        private void LoadStoryData()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var story = context.Stories.FirstOrDefault(s => s.Id == storyId);
                    if (story != null)
                    {
                        txtTitle.Text = story.Title;
                        txtContent.Text = story.Content;

                        if (story.EventDate.HasValue)
                            dpEventDate.SelectedDate = story.EventDate.Value;
                        else if (!string.IsNullOrEmpty(story.EventDateText))
                            txtEventDateText.Text = story.EventDateText;

                        var mediaLinks = context.MediaLinks
                            .Where(ml => ml.StoryId == storyId)
                            .Select(ml => ml.MediaFileId)
                            .ToList();

                        var mediaFiles = context.MediaFiles
                            .Where(mf => mediaLinks.Contains(mf.Id))
                            .ToList();

                        foreach (var file in mediaFiles)
                        {
                            attachedMedia.Add(new AttachedMedia
                            {
                                FileId = file.Id,
                                FileName = file.FileName,
                                FilePath = file.FilePath,
                                MediaTypeId = file.MediaTypeId,
                                Icon = GetIconForMediaType(file.MediaTypeId),
                                IsNew = false
                            });
                        }

                        lvAttachedMedia.ItemsSource = attachedMedia;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки истории: {ex.Message}");
            }
        }

        private string GetIconForMediaType(int mediaTypeId)
        {
            switch (mediaTypeId)
            {
                case 1: return "📷";
                case 2: return "🎥";
                case 3: return "🎵";
                default: return "📁";
            }
        }

        private void ValidateFields(object sender, TextChangedEventArgs e)
        {
            bool isValid = !string.IsNullOrWhiteSpace(txtTitle.Text) &&
                          !string.IsNullOrWhiteSpace(txtContent.Text);

            // Проверка длины полей
            if (txtTitle.Text.Length > MAX_TITLE_LENGTH)
            {
                isValid = false;
                ShowTitleLengthWarning();
            }
            else
            {
                ResetTitleWarning();
            }

            if (txtContent.Text.Length > MAX_CONTENT_LENGTH)
            {
                isValid = false;
                ShowContentLengthWarning();
            }
            else
            {
                ResetContentWarning();
            }

            if (txtEventDateText.Text.Length > MAX_DATE_TEXT_LENGTH)
            {
                isValid = false;
                ShowDateTextLengthWarning();
            }
            else
            {
                ResetDateTextWarning();
            }

            btnSave.IsEnabled = isValid;
        }

        private void ShowTitleLengthWarning()
        {
            txtTitle.BorderBrush = System.Windows.Media.Brushes.Red;
            txtTitle.ToolTip = $"Максимальная длина заголовка - {MAX_TITLE_LENGTH} символов";
        }

        private void ResetTitleWarning()
        {
            txtTitle.BorderBrush = System.Windows.Media.Brushes.LightGray;
            txtTitle.ToolTip = null;
        }

        private void ShowContentLengthWarning()
        {
            txtContent.BorderBrush = System.Windows.Media.Brushes.Red;
            txtContent.ToolTip = $"Максимальная длина текста - {MAX_CONTENT_LENGTH} символов";
        }

        private void ResetContentWarning()
        {
            txtContent.BorderBrush = System.Windows.Media.Brushes.LightGray;
            txtContent.ToolTip = null;
        }

        private void ShowDateTextLengthWarning()
        {
            txtEventDateText.BorderBrush = System.Windows.Media.Brushes.Red;
            txtEventDateText.ToolTip = $"Максимальная длина описания даты - {MAX_DATE_TEXT_LENGTH} символов";
        }

        private void ResetDateTextWarning()
        {
            txtEventDateText.BorderBrush = System.Windows.Media.Brushes.LightGray;
            txtEventDateText.ToolTip = null;
        }

        // ==================== ДОБАВЛЕНИЕ МЕДИАФАЙЛОВ ====================

        private void AddPhoto_Click(object sender, RoutedEventArgs e)
        {
            AddMediaFile("Изображения|*.jpg;*.jpeg;*.png;*.gif;*.bmp", 1);
        }

        private void AddVideo_Click(object sender, RoutedEventArgs e)
        {
            AddMediaFile("Видео|*.mp4;*.avi;*.mov;*.wmv", 2);
        }

        private void AddAudio_Click(object sender, RoutedEventArgs e)
        {
            AddMediaFile("Аудио|*.mp3;*.wav;*.wma", 3);
        }

        private void AddMediaFile(string filter, int mediaTypeId)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите файл",
                Filter = filter + "|Все файлы|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                string fileName = System.IO.Path.GetFileName(dialog.FileName);

                if (attachedMedia.Any(m => m.FileName == fileName))
                {
                    MessageBox.Show("Файл с таким именем уже прикреплен!", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                attachedMedia.Add(new AttachedMedia
                {
                    FileId = -attachedMedia.Count - 1,
                    FileName = fileName,
                    FilePath = dialog.FileName,
                    MediaTypeId = mediaTypeId,
                    Icon = GetIconForMediaType(mediaTypeId),
                    IsNew = true
                });

                lvAttachedMedia.ItemsSource = null;
                lvAttachedMedia.ItemsSource = attachedMedia;
            }
        }

        private void RemoveMedia_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            int fileId = (int)button.Tag;
            var mediaToRemove = attachedMedia.FirstOrDefault(m => m.FileId == fileId);

            if (mediaToRemove != null)
            {
                attachedMedia.Remove(mediaToRemove);
                lvAttachedMedia.ItemsSource = null;
                lvAttachedMedia.ItemsSource = attachedMedia;
            }
        }

        // ==================== СОХРАНЕНИЕ ФАЙЛОВ ====================

        private string GetMediaFolderPath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string projectDirectory = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(baseDirectory, @"..\..\..\"));
            string mediaFolder = System.IO.Path.Combine(projectDirectory, "Media");

            if (!Directory.Exists(mediaFolder))
                Directory.CreateDirectory(mediaFolder);

            return mediaFolder;
        }

        private string SaveFileToAppFolder(string sourcePath)
        {
            try
            {
                string mediaFolder = GetMediaFolderPath();
                string fileExtension = System.IO.Path.GetExtension(sourcePath);
                string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                string destPath = System.IO.Path.Combine(mediaFolder, uniqueFileName);

                File.Copy(sourcePath, destPath);

                return $@"Media\{uniqueFileName}";
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при сохранении файла: {ex.Message}");
            }
        }

        // ==================== СОХРАНЕНИЕ ИСТОРИИ ====================

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSave();
        }

        private void PerformSave()
        {
            // Предотвращаем повторный вызов
            if (_isProcessing)
                return;

            try
            {
                _isProcessing = true;

                // Проверка обязательных полей
                if (string.IsNullOrWhiteSpace(txtTitle.Text) || string.IsNullOrWhiteSpace(txtContent.Text))
                {
                    MessageBox.Show("Заполните заголовок и текст истории!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка длины полей перед сохранением
                if (txtTitle.Text.Length > MAX_TITLE_LENGTH)
                {
                    MessageBox.Show($"Заголовок слишком длинный! Максимум {MAX_TITLE_LENGTH} символов.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtTitle.Focus();
                    return;
                }

                if (txtContent.Text.Length > MAX_CONTENT_LENGTH)
                {
                    MessageBox.Show($"Текст истории слишком длинный! Максимум {MAX_CONTENT_LENGTH} символов.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtContent.Focus();
                    return;
                }

                if (txtEventDateText.Text.Length > MAX_DATE_TEXT_LENGTH)
                {
                    MessageBox.Show($"Описание даты слишком длинное! Максимум {MAX_DATE_TEXT_LENGTH} символов.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtEventDateText.Focus();
                    return;
                }

                // Проверка даты (не из будущего)
                if (dpEventDate.SelectedDate.HasValue && dpEventDate.SelectedDate.Value > DateTime.Today)
                {
                    MessageBox.Show("Дата события не может быть в будущем!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    dpEventDate.Focus();
                    return;
                }

                using (var context = new GenealogyDBEntities())
                {
                    Stories story;

                    if (storyId.HasValue)
                    {
                        story = context.Stories.FirstOrDefault(s => s.Id == storyId);
                        if (story == null)
                        {
                            MessageBox.Show("История не найдена!", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        story.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        story = new Stories
                        {
                            PersonId = personId,
                            CreatedByUserId = Session.UserId,
                            CreatedAt = DateTime.Now
                        };
                        context.Stories.Add(story);
                    }

                    story.Title = txtTitle.Text.Trim();
                    story.Content = txtContent.Text.Trim();

                    if (dpEventDate.SelectedDate.HasValue)
                    {
                        story.EventDate = dpEventDate.SelectedDate.Value;
                        story.EventDateText = null;
                    }
                    else if (!string.IsNullOrWhiteSpace(txtEventDateText.Text))
                    {
                        story.EventDate = null;
                        story.EventDateText = txtEventDateText.Text.Trim();
                    }
                    else
                    {
                        story.EventDate = null;
                        story.EventDateText = null;
                    }

                    context.SaveChanges();

                    int currentStoryId = story.Id;
                    ProcessMediaFiles(context, currentStoryId);

                    string message = storyId.HasValue ? "История обновлена!" : "История добавлена!";
                    MessageBox.Show(message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    NavigationService.GoBack();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void ProcessMediaFiles(GenealogyDBEntities context, int storyId)
        {
            if (this.storyId.HasValue)
            {
                var oldLinks = context.MediaLinks.Where(ml => ml.StoryId == this.storyId).ToList();
                context.MediaLinks.RemoveRange(oldLinks);
            }

            foreach (var media in attachedMedia)
            {
                int mediaFileId;

                if (media.IsNew)
                {
                    string relativePath = SaveFileToAppFolder(media.FilePath);

                    var mediaFile = new MediaFiles
                    {
                        FileName = media.FileName,
                        FilePath = relativePath,
                        FileSize = new FileInfo(media.FilePath).Length,
                        MediaTypeId = media.MediaTypeId,
                        Description = $"Прикреплено к истории ID: {storyId}",
                        UploadedByUserId = Session.UserId,
                        UploadedAt = DateTime.Now,
                        IsProfilePhoto = false
                    };

                    context.MediaFiles.Add(mediaFile);
                    context.SaveChanges();
                    mediaFileId = mediaFile.Id;
                }
                else
                {
                    mediaFileId = media.FileId;
                }

                var link = new MediaLinks
                {
                    MediaFileId = mediaFileId,
                    PersonId = null,
                    StoryId = storyId,
                    SortOrder = attachedMedia.IndexOf(media)
                };
                context.MediaLinks.Add(link);
            }

            context.SaveChanges();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}