using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Genealogy.AppData;

namespace Genealogy.Pages
{
    public partial class CalendarWindow : Window
    {
        private int treeId;

        public class BirthdayItem
        {
            public string FullName { get; set; }
            public string DayMonth { get; set; }
            public string AgeText { get; set; }
            public string BackgroundColor { get; set; }
            public DateTime BirthDate { get; set; }
            public int Month { get; set; }
            public int Day { get; set; }
        }

        public CalendarWindow(int treeId, string treeName)
        {
            InitializeComponent();
            this.treeId = treeId;

            txtTreeInfo.Text = $"Древо: {treeName}";

            Loaded += CalendarWindow_Loaded;
        }

        private void CalendarWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadBirthdays();
        }

        private void LoadBirthdays()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var persons = context.Persons
                        .Where(p => p.TreeId == treeId && p.BirthDate.HasValue)
                        .ToList();

                    if (!persons.Any())
                    {
                        lvBirthdays.ItemsSource = null;
                        return;
                    }

                    var today = DateTime.Today;
                    var birthdays = new List<BirthdayItem>();

                    foreach (var person in persons)
                    {
                        var birthDate = person.BirthDate.Value;

                        // Форматируем дату
                        string dayMonth = birthDate.ToString("dd.MM");

                        // Вычисляем возраст
                        int age = today.Year - birthDate.Year;
                        if (today < birthDate.AddYears(age)) age--;

                        // Проверяем, сегодня ли день рождения
                        bool isToday = birthDate.Month == today.Month && birthDate.Day == today.Day;

                        // Цвет фона
                        string bgColor = "#FDF8F0";
                        if (isToday)
                            bgColor = "#FFE4B5"; // Светло-золотистый

                        birthdays.Add(new BirthdayItem
                        {
                            FullName = $"{person.LastName} {person.FirstName} {person.Patronymic}".Trim(),
                            DayMonth = dayMonth,
                            AgeText = $"{age} лет",
                            BackgroundColor = bgColor,
                            BirthDate = birthDate,
                            Month = birthDate.Month,
                            Day = birthDate.Day
                        });
                    }

                    // Сортируем по месяцу и дню
                    birthdays = birthdays
                        .OrderBy(b => b.Month)
                        .ThenBy(b => b.Day)
                        .ToList();

                    lvBirthdays.ItemsSource = birthdays;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}