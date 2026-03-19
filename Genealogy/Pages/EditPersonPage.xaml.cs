using Genealogy.Classes;
using Microsoft.Win32;
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
    public partial class EditPersonPage : Page
    {
        private int? editPersonId = null;
        private int currentTreeId = 1; // Значение по умолчанию, будет заменено в Page_Loaded
        private string selectedPhotoPath = null;
        private List<PersonComboItem> allPersons = new List<PersonComboItem>();

        // Класс для ComboBox
        public class PersonComboItem
        {
            public int Id { get; set; }
            public string DisplayName { get; set; }
        }

        public EditPersonPage()
        {
            InitializeComponent();
            txtTitle.Text = "ДОБАВЛЕНИЕ ПЕРСОНЫ";
        }

        public EditPersonPage(int personId) : this()
        {
            editPersonId = personId;
            txtTitle.Text = "РЕДАКТИРОВАНИЕ ПЕРСОНЫ";
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Загружаем текущее дерево из сессии
            if (Session.CurrentTreeId > 0)
            {
                currentTreeId = Session.CurrentTreeId;
            }
            else
            {
                // Если нет текущего дерева, пробуем найти первое доступное
                using (var context = new GenealogyDBEntities())
                {
                    var firstTree = context.FamilyTrees
                        .Where(t => t.CreatedByUserId == Session.UserId)
                        .FirstOrDefault();

                    if (firstTree != null)
                    {
                        currentTreeId = firstTree.Id;
                        Session.CurrentTreeId = firstTree.Id;
                    }
                    else
                    {
                        MessageBox.Show("У вас нет доступных деревьев. Сначала создайте дерево.",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        NavigationService.GoBack();
                        return;
                    }
                }
            }

            LoadPersonsForCombo();

            if (editPersonId.HasValue)
            {
                LoadPersonData(editPersonId.Value);
            }
        }

        private void LoadPersonsForCombo()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var persons = context.Persons
                        .Where(p => p.TreeId == currentTreeId)
                        .ToList();

                    allPersons = persons.Select(p => new PersonComboItem
                    {
                        Id = p.Id,
                        DisplayName = $"{p.LastName} {p.FirstName} {p.Patronymic} ({p.BirthDate?.Year})".Trim()
                    }).ToList();

                    // Добавляем пустой элемент в начало
                    allPersons.Insert(0, new PersonComboItem { Id = 0, DisplayName = "— Не выбрано —" });

                    cmbFather.ItemsSource = allPersons;
                    cmbMother.ItemsSource = allPersons;
                    cmbSpouse.ItemsSource = allPersons;

                    // Устанавливаем DisplayMemberPath после установки ItemsSource
                    cmbFather.DisplayMemberPath = "DisplayName";
                    cmbFather.SelectedValuePath = "Id";

                    cmbMother.DisplayMemberPath = "DisplayName";
                    cmbMother.SelectedValuePath = "Id";

                    cmbSpouse.DisplayMemberPath = "DisplayName";
                    cmbSpouse.SelectedValuePath = "Id";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка персон: {ex.Message}");
            }
        }

        private void LoadPersonData(int personId)
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var person = context.Persons.FirstOrDefault(p => p.Id == personId);
                    if (person == null)
                    {
                        MessageBox.Show("Персона не найдена!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Устанавливаем текущее дерево из данных персоны (на случай, если редактируем персону из другого дерева)
                    currentTreeId = person.TreeId;

                    // Заполняем основные поля
                    txtLastName.Text = person.LastName;
                    txtFirstName.Text = person.FirstName;
                    txtPatronymic.Text = person.Patronymic;
                    txtMaidenName.Text = person.MaidenName;

                    // Устанавливаем пол
                    if (person.GenderId == 1)
                        cmbGender.SelectedIndex = 0;
                    else if (person.GenderId == 2)
                        cmbGender.SelectedIndex = 1;
                    else
                        cmbGender.SelectedIndex = 2;

                    // Даты
                    if (person.BirthDate.HasValue)
                        dpBirthDate.SelectedDate = person.BirthDate.Value;

                    if (person.DeathDate.HasValue)
                        dpDeathDate.SelectedDate = person.DeathDate.Value;

                    // Места
                    txtBirthPlace.Text = person.BirthPlace;
                    txtDeathPlace.Text = person.DeathPlace;

                    // Биография
                    txtBiography.Text = person.Biography;

                    // Перезагружаем список персон для текущего дерева
                    LoadPersonsForCombo();

                    // Загрузка связей (родители и супруг)
                    LoadPersonRelationships(personId, context);

                    // Фото
                    if (!string.IsNullOrEmpty(person.ProfilePhotoPath))
                    {
                        selectedPhotoPath = person.ProfilePhotoPath;
                        ShowPhotoPreview(selectedPhotoPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPersonRelationships(int personId, GenealogyDBEntities context)
        {
            try
            {
                // Загружаем отца (родитель с типом связи 1, где текущая персона - ребенок)
                var fatherRel = context.Relationships
                    .FirstOrDefault(r => r.Person2Id == personId && r.RelationshipType == 1);

                if (fatherRel != null && fatherRel.Person1Id > 0)
                {
                    // Проверяем, есть ли такой ID в списке allPersons
                    if (allPersons.Any(p => p.Id == fatherRel.Person1Id))
                        cmbFather.SelectedValue = fatherRel.Person1Id;
                }

                // Загружаем мать (родитель с типом связи 1, где текущая персона - ребенок)
                // Примечание: в БД может быть несколько записей для родителей, 
                // нам нужно найти именно мать (женский пол)
                var allParentRels = context.Relationships
                    .Where(r => r.Person2Id == personId && r.RelationshipType == 1)
                    .ToList();

                foreach (var rel in allParentRels)
                {
                    var parent = context.Persons.FirstOrDefault(p => p.Id == rel.Person1Id);
                    if (parent != null)
                    {
                        // Если родитель женского пола (GenderId = 2) - это мать
                        if (parent.GenderId == 2)
                        {
                            if (allPersons.Any(p => p.Id == parent.Id))
                                cmbMother.SelectedValue = parent.Id;
                        }
                        // Если родитель мужского пола и мы еще не нашли отца
                        else if (parent.GenderId == 1 && cmbFather.SelectedValue == null)
                        {
                            if (allPersons.Any(p => p.Id == parent.Id))
                                cmbFather.SelectedValue = parent.Id;
                        }
                    }
                }

                // Загружаем супруга(у)
                var spouseRel = context.Relationships
                    .FirstOrDefault(r => (r.Person1Id == personId || r.Person2Id == personId)
                                      && r.RelationshipType == 2);

                if (spouseRel != null)
                {
                    int spouseId = spouseRel.Person1Id == personId ?
                        spouseRel.Person2Id : spouseRel.Person1Id;

                    if (spouseId > 0 && allPersons.Any(p => p.Id == spouseId))
                        cmbSpouse.SelectedValue = spouseId;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки родственных связей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowPhotoPreview(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                imgPreview.Source = bitmap;
                imgPreview.Visibility = Visibility.Visible;
                txtNoImage.Visibility = Visibility.Collapsed;
                btnRemovePhoto.IsEnabled = true;
            }
            catch
            {
                imgPreview.Visibility = Visibility.Collapsed;
                txtNoImage.Visibility = Visibility.Visible;
                btnRemovePhoto.IsEnabled = false;
            }
        }

        private void SelectPhoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите фотографию",
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Все файлы|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedPhotoPath = dialog.FileName;
                ShowPhotoPreview(selectedPhotoPath);
            }
        }

        private void RemovePhoto_Click(object sender, RoutedEventArgs e)
        {
            selectedPhotoPath = null;
            imgPreview.Source = null;
            imgPreview.Visibility = Visibility.Collapsed;
            txtNoImage.Visibility = Visibility.Visible;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверка обязательных полей
                if (string.IsNullOrWhiteSpace(txtLastName.Text) ||
                    string.IsNullOrWhiteSpace(txtFirstName.Text))
                {
                    MessageBox.Show("Заполните обязательные поля (Фамилия и Имя)!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (var context = new GenealogyDBEntities())
                {
                    Persons person;

                    if (editPersonId.HasValue)
                    {
                        // Редактирование существующей персоны
                        person = context.Persons.FirstOrDefault(p => p.Id == editPersonId);
                        if (person == null)
                        {
                            MessageBox.Show("Персона не найдена!", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    else
                    {
                        // Добавление новой персоны
                        person = new Persons
                        {
                            TreeId = currentTreeId, // Используем текущее дерево из сессии
                            CreatedByUserId = Session.UserId,
                            CreatedAt = DateTime.Now
                        };
                        context.Persons.Add(person);
                    }

                    // Заполнение данных
                    person.LastName = txtLastName.Text.Trim();
                    person.FirstName = txtFirstName.Text.Trim();
                    person.Patronymic = string.IsNullOrWhiteSpace(txtPatronymic.Text) ? null : txtPatronymic.Text.Trim();
                    person.MaidenName = string.IsNullOrWhiteSpace(txtMaidenName.Text) ? null : txtMaidenName.Text.Trim();

                    // Установка пола
                    if (cmbGender.SelectedIndex == 0)
                        person.GenderId = 1;
                    else if (cmbGender.SelectedIndex == 1)
                        person.GenderId = 2;
                    else
                        person.GenderId = 3;

                    // Даты
                    person.BirthDate = dpBirthDate.SelectedDate;
                    person.DeathDate = dpDeathDate.SelectedDate;

                    // Места
                    person.BirthPlace = string.IsNullOrWhiteSpace(txtBirthPlace.Text) ? null : txtBirthPlace.Text.Trim();
                    person.DeathPlace = string.IsNullOrWhiteSpace(txtDeathPlace.Text) ? null : txtDeathPlace.Text.Trim();

                    // Биография
                    person.Biography = string.IsNullOrWhiteSpace(txtBiography.Text) ? null : txtBiography.Text.Trim();

                    // Фото
                    if (!string.IsNullOrEmpty(selectedPhotoPath))
                    {
                        // В реальном проекте нужно копировать файл в папку приложения
                        person.ProfilePhotoPath = selectedPhotoPath;
                    }

                    person.UpdatedAt = DateTime.Now;

                    // Сохраняем изменения персоны
                    context.SaveChanges();

                    // Обновляем родственные связи
                    SaveRelationships(context, person.Id);

                    string message = editPersonId.HasValue ? "Изменения сохранены!" : "Персона добавлена!";
                    MessageBox.Show(message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Возвращаемся на главную страницу
                    NavigationService.GoBack();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveRelationships(GenealogyDBEntities context, int personId)
        {
            try
            {
                // Удаляем старые связи (только если это редактирование)
                if (editPersonId.HasValue)
                {
                    var oldRelations = context.Relationships
                        .Where(r => r.Person1Id == personId || r.Person2Id == personId)
                        .ToList();
                    context.Relationships.RemoveRange(oldRelations);
                    context.SaveChanges();
                }

                // Добавляем отца
                if (cmbFather.SelectedValue != null && (int)cmbFather.SelectedValue > 0)
                {
                    context.Relationships.Add(new Relationships
                    {
                        Person1Id = (int)cmbFather.SelectedValue,
                        Person2Id = personId,
                        RelationshipType = 1,
                        Direction = 1,
                        CreatedByUserId = Session.UserId,
                        CreatedAt = DateTime.Now
                    });
                }

                // Добавляем мать
                if (cmbMother.SelectedValue != null && (int)cmbMother.SelectedValue > 0)
                {
                    context.Relationships.Add(new Relationships
                    {
                        Person1Id = (int)cmbMother.SelectedValue,
                        Person2Id = personId,
                        RelationshipType = 1,
                        Direction = 1,
                        CreatedByUserId = Session.UserId,
                        CreatedAt = DateTime.Now
                    });
                }

                // Добавляем супруга(у)
                if (cmbSpouse.SelectedValue != null && (int)cmbSpouse.SelectedValue > 0)
                {
                    int spouseId = (int)cmbSpouse.SelectedValue;

                    // Добавляем связь в обе стороны для супругов
                    context.Relationships.Add(new Relationships
                    {
                        Person1Id = personId,
                        Person2Id = spouseId,
                        RelationshipType = 2,
                        CreatedByUserId = Session.UserId,
                        CreatedAt = DateTime.Now
                    });
                }

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка сохранения родственных связей: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void FindFather_Click(object sender, RoutedEventArgs e)
        {
            cmbFather.IsDropDownOpen = true;
        }

        private void FindMother_Click(object sender, RoutedEventArgs e)
        {
            cmbMother.IsDropDownOpen = true;
        }

        private void FindSpouse_Click(object sender, RoutedEventArgs e)
        {
            cmbSpouse.IsDropDownOpen = true;
        }
    }
}