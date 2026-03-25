using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;

namespace Genealogy.Services
{
    public class MediaService
    {
        private readonly string _mediaFolderPath;

        // Допустимые расширения файлов для каждого типа
        private static readonly Dictionary<int, List<string>> _allowedExtensions = new Dictionary<int, List<string>>
        {
            { 1, new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp" } },        // Фото
            { 2, new List<string> { ".mp4", ".avi", ".mov", ".wmv", ".mkv" } },         // Видео
            { 3, new List<string> { ".mp3", ".wav", ".wma", ".flac" } }                 // Аудио
        };

        public MediaService()
        {
            // Определяем путь к папке Media
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Если мы в папке Debug/Release, поднимаемся на уровень выше
            if (baseDirectory.Contains("\\bin\\"))
            {
                baseDirectory = Directory.GetParent(baseDirectory).Parent.Parent.FullName;
            }

            _mediaFolderPath = Path.Combine(baseDirectory, "Media");

            // Создаём папку Media, если её нет
            if (!Directory.Exists(_mediaFolderPath))
            {
                Directory.CreateDirectory(_mediaFolderPath);
            }
        }

        /// <summary>
        /// Проверяет, подходит ли файл по расширению
        /// </summary>
        public bool IsValidFileType(string filePath, int mediaTypeId)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (_allowedExtensions.ContainsKey(mediaTypeId))
            {
                return _allowedExtensions[mediaTypeId].Contains(extension);
            }

            return false;
        }

        /// <summary>
        /// Получает строку фильтра для диалога выбора файла
        /// </summary>
        public string GetAllowedExtensionsFilter(int mediaTypeId)
        {
            if (!_allowedExtensions.ContainsKey(mediaTypeId))
                return "*.*";

            string filter = string.Join(";", _allowedExtensions[mediaTypeId].Select(ext => $"*{ext}"));

            if (mediaTypeId == 1) return $"Изображения|{filter}";
            if (mediaTypeId == 2) return $"Видео|{filter}";
            if (mediaTypeId == 3) return $"Аудио|{filter}";

            return "Все файлы|*.*";
        }

        /// <summary>
        /// Вычисляет хэш файла (для проверки дубликатов)
        /// </summary>
        public string ComputeFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Сохраняет файл в папку Media
        /// </summary>
        /// <returns>Относительный путь к сохранённому файлу</returns>
        public async Task<string> SaveMediaFileAsync(string sourcePath, int mediaTypeId)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Исходный файл не найден", sourcePath);

            if (!IsValidFileType(sourcePath, mediaTypeId))
                throw new InvalidOperationException($"Файл не соответствует выбранному типу медиа");

            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();

            // Генерируем уникальное имя файла
            string uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            string destPath = Path.Combine(_mediaFolderPath, uniqueFileName);

            // Копируем файл
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            using (var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                await sourceStream.CopyToAsync(destStream);
            }

            // Возвращаем относительный путь (для сохранения в БД)
            return $"Media/{uniqueFileName}";
        }

        /// <summary>
        /// Получить полный путь к файлу по относительному пути
        /// </summary>
        public string GetFullPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Если мы в папке Debug/Release, поднимаемся на уровень выше
            if (baseDir.Contains("\\bin\\"))
            {
                baseDir = Directory.GetParent(baseDir).Parent.Parent.FullName;
            }

            // Заменяем слеши на обратные слеши для Windows
            string cleanPath = relativePath.Replace('/', '\\');

            // Если путь уже абсолютный
            if (Path.IsPathRooted(cleanPath))
                return cleanPath;

            return Path.Combine(baseDir, cleanPath);
        }

        /// <summary>
        /// Удаляет файл по относительному пути
        /// </summary>
        public void DeleteMediaFile(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return;

            string fullPath = GetFullPath(relativePath);

            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch
                {
                    // Если не удалось удалить, просто игнорируем
                }
            }
        }

        /// <summary>
        /// Получает размер файла в удобном формате
        /// </summary>
        public string GetFileSizeString(long bytes)
        {
            string[] sizes = { "Б", "КБ", "МБ", "ГБ" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}