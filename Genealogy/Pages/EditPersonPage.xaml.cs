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
using System.IO;

namespace Genealogy.Pages
{
    public partial class EditPersonPage : Page
    {
        private int? editPersonId = null;
        private int currentTreeId = 1;
        private string selectedPhotoPath = null;
        private string originalPhotoPath = null;
        private List<PersonComboItem> allPersons = new List<PersonComboItem>();

        // Ограничения на длину полей
        private const int MAX_LASTNAME_LENGTH = 100;
        private const int MAX_FIRSTNAME_LENGTH = 100;
        private const int MAX_PATRONYMIC_LENGTH = 100;
        private const int MAX_MAIDENNAME_LENGTH = 100;
        private const int MAX_BIRTHPLACE_LENGTH = 200;
        private const int MAX_DEATHPLACE_LENGTH = 200;
        private const int MAX_BIOGRAPHY_LENGTH = 5000;

        public class PersonComboItem
        {
            public int Id { get; set; }
            public string DisplayName { get; set; }
        }

        public EditPersonPage()
        {
            InitializeComponent();
            txtTitle.Text = "ДОБАВЛЕНИЕ ПЕРСОНЫ";
            SetupTextValidation();
        }

        public EditPersonPage(int personId) : this()
        {
            editPersonId = personId;
            txtTitle.Text = "РЕДАКТИРОВАНИЕ ПЕРСОНЫ";
        }

        private void SetupTextValidation()
        {
            // Устанавливаем максимальную длину для текстовых полей
            txtLastName.MaxLength = MAX_LASTNAME_LENGTH;
            txtFirstName.MaxLength = MAX_FIRSTNAME_LENGTH;
            txtPatronymic.MaxLength = MAX_PATRONYMIC_LENGTH;
            txtMaidenName.MaxLength = MAX_MAIDENNAME_LENGTH;
            txtBirthPlace.MaxLength = MAX_BIRTHPLACE_LENGTH;
            txtDeathPlace.MaxLength = MAX_DEATHPLACE_LENGTH;
            txtBiography.MaxLength = MAX_BIOGRAPHY_LENGTH;
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

            // Устанавливаем ограничения на даты
            dpBirthDate.SelectedDateChanged += BirthDate_SelectedDateChanged;
            dpDeathDate.SelectedDateChanged += DeathDate_SelectedDateChanged;

            dpBirthDate.DisplayDateEnd = DateTime.Today;
            dpDeathDate.DisplayDateEnd = DateTime.Today;

            LoadPersonsForCombo();

            if (editPersonId.HasValue)
            {
                LoadPersonData(editPersonId.Value);
            }
        }

        private void BirthDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpBirthDate.SelectedDate.HasValue && dpBirthDate.SelectedDate.Value > DateTime.Today)
            {
                MessageBox.Show("Дата рождения не может быть в будущем!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                dpBirthDate.SelectedDate = null;
            }

            // Проверяем, что дата смерти не раньше даты рождения
            if (dpBirthDate.SelectedDate.HasValue && dpDeathDate.SelectedDate.HasValue)
            {
                if (dpDeathDate.SelectedDate.Value < dpBirthDate.SelectedDate.Value)
                {
                    MessageBox.Show("Дата смерти не может быть раньше даты рождения!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    dpDeathDate.SelectedDate = null;
                }
            }
        }

        private void DeathDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpDeathDate.SelectedDate.HasValue && dpDeathDate.SelectedDate.Value > DateTime.Today)
            {
                MessageBox.Show("Дата смерти не может быть в будущем!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                dpDeathDate.SelectedDate = null;
                return;
            }

            if (dpBirthDate.SelectedDate.HasValue && dpDeathDate.SelectedDate.HasValue)
            {
                if (dpDeathDate.SelectedDate.Value < dpBirthDate.SelectedDate.Value)
                {
                    MessageBox.Show("Дата смерти не может быть раньше даты рождения!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    dpDeathDate.SelectedDate = null;
                }
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

                    allPersons.Insert(0, new PersonComboItem { Id = 0, DisplayName = "— Не выбрано —" });

                    cmbFather.ItemsSource = allPersons;
                    cmbMother.ItemsSource = allPersons;
                    cmbSpouse.ItemsSource = allPersons;

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

                    currentTreeId = person.TreeId;

                    txtLastName.Text = person.LastName;
                    txtFirstName.Text = person.FirstName;
                    txtPatronymic.Text = person.Patronymic;
                    txtMaidenName.Text = person.MaidenName;

                    if (person.GenderId == 1)
                        cmbGender.SelectedIndex = 0;
                    else if (person.GenderId == 2)
                        cmbGender.SelectedIndex = 1;
                    else
                        cmbGender.SelectedIndex = 2;

                    if (person.BirthDate.HasValue)
                        dpBirthDate.SelectedDate = person.BirthDate.Value;

                    if (person.DeathDate.HasValue)
                        dpDeathDate.SelectedDate = person.DeathDate.Value;

                    txtBirthPlace.Text = person.BirthPlace;
                    txtDeathPlace.Text = person.DeathPlace;
                    txtBiography.Text = person.Biography;

                    LoadPersonsForCombo();
                    LoadPersonRelationships(personId, context);

                    if (!string.IsNullOrEmpty(person.ProfilePhotoPath))
                    {
                        selectedPhotoPath = person.ProfilePhotoPath;
                        originalPhotoPath = person.ProfilePhotoPath;
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
                var allParentRels = context.Relationships
                    .Where(r => r.Person2Id == personId && r.RelationshipType == 1)
                    .ToList();

                foreach (var rel in allParentRels)
                {
                    var parent = context.Persons.FirstOrDefault(p => p.Id == rel.Person1Id);
                    if (parent != null)
                    {
                        if (parent.GenderId == 2)
                        {
                            if (allPersons.Any(p => p.Id == parent.Id))
                                cmbMother.SelectedValue = parent.Id;
                        }
                        else if (parent.GenderId == 1 && (cmbFather.SelectedValue == null || (int)cmbFather.SelectedValue == 0))
                        {
                            if (allPersons.Any(p => p.Id == parent.Id))
                                cmbFather.SelectedValue = parent.Id;
                        }
                    }
                }

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

        private bool ValidateRelationships()
        {
            int fatherId = cmbFather.SelectedValue != null ? (int)cmbFather.SelectedValue : 0;
            int motherId = cmbMother.SelectedValue != null ? (int)cmbMother.SelectedValue : 0;
            int spouseId = cmbSpouse.SelectedValue != null ? (int)cmbSpouse.SelectedValue : 0;

            // Проверка на дублирование (отец, мать и супруг не могут быть одним человеком)
            if (fatherId != 0 && motherId != 0 && fatherId == motherId)
            {
                MessageBox.Show("Отец и мать не могут быть одним человеком!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (fatherId != 0 && spouseId != 0 && fatherId == spouseId)
            {
                MessageBox.Show("Отец и супруг(а) не могут быть одним человеком!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (motherId != 0 && spouseId != 0 && motherId == spouseId)
            {
                MessageBox.Show("Мать и супруг(а) не могут быть одним человеком!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Проверка, что персона не выбирает саму себя
            if (editPersonId.HasValue)
            {
                int currentPersonId = editPersonId.Value;

                if (fatherId == currentPersonId)
                {
                    MessageBox.Show("Персона не может быть своим отцом!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (motherId == currentPersonId)
                {
                    MessageBox.Show("Персона не может быть своей матерью!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (spouseId == currentPersonId)
                {
                    MessageBox.Show("Персона не может быть своим супругом!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private void ShowPhotoPreview(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    imgPreview.Source = bitmap;
                    imgPreview.Visibility = Visibility.Visible;
                    txtNoImage.Visibility = Visibility.Collapsed;
                    btnRemovePhoto.IsEnabled = true;
                }
                else
                {
                    imgPreview.Visibility = Visibility.Collapsed;
                    txtNoImage.Visibility = Visibility.Visible;
                    btnRemovePhoto.IsEnabled = false;
                }
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
            btnRemovePhoto.IsEnabled = false;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Валидация обязательных полей
                if (string.IsNullOrWhiteSpace(txtLastName.Text))
                {
                    MessageBox.Show("Заполните поле Фамилия!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtLastName.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtFirstName.Text))
                {
                    MessageBox.Show("Заполните поле Имя!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtFirstName.Focus();
                    return;
                }

                // Валидация длинны текста
                if (txtLastName.Text.Length > MAX_LASTNAME_LENGTH)
                {
                    MessageBox.Show($"Фамилия не может быть больше {MAX_LASTNAME_LENGTH} символов!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtLastName.Focus();
                    return;
                }

                if (txtFirstName.Text.Length > MAX_FIRSTNAME_LENGTH)
                {
                    MessageBox.Show($"Имя не может быть больше {MAX_FIRSTNAME_LENGTH} символов!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtFirstName.Focus();
                    return;
                }

                if (txtPatronymic.Text.Length > MAX_PATRONYMIC_LENGTH)
                {
                    MessageBox.Show($"Отчество не может быть больше {MAX_PATRONYMIC_LENGTH} символов!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtPatronymic.Focus();
                    return;
                }

                if (txtMaidenName.Text.Length > MAX_MAIDENNAME_LENGTH)
                {
                    MessageBox.Show($"Девичья фамилия не может быть больше {MAX_MAIDENNAME_LENGTH} символов!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtMaidenName.Focus();
                    return;
                }

                if (txtBirthPlace.Text.Length > MAX_BIRTHPLACE_LENGTH)
                {
                    MessageBox.Show($"Место рождения не может быть больше {MAX_BIRTHPLACE_LENGTH} символов!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtBirthPlace.Focus();
                    return;
                }

                if (txtDeathPlace.Text.Length > MAX_DEATHPLACE_LENGTH)
                {
                    MessageBox.Show($"Место смерти не может быть больше {MAX_DEATHPLACE_LENGTH} символов!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtDeathPlace.Focus();
                    return;
                }

                if (txtBiography.Text.Length > MAX_BIOGRAPHY_LENGTH)
                {
                    MessageBox.Show($"Биография не может быть больше {MAX_BIOGRAPHY_LENGTH} символов!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtBiography.Focus();
                    return;
                }

                // Валидация дат
                if (dpBirthDate.SelectedDate.HasValue && dpBirthDate.SelectedDate.Value > DateTime.Today)
                {
                    MessageBox.Show("Дата рождения не может быть в будущем!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    dpBirthDate.Focus();
                    return;
                }

                if (dpDeathDate.SelectedDate.HasValue && dpDeathDate.SelectedDate.Value > DateTime.Today)
                {
                    MessageBox.Show("Дата смерти не может быть в будущем!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    dpDeathDate.Focus();
                    return;
                }

                if (dpBirthDate.SelectedDate.HasValue && dpDeathDate.SelectedDate.HasValue)
                {
                    if (dpDeathDate.SelectedDate.Value < dpBirthDate.SelectedDate.Value)
                    {
                        MessageBox.Show("Дата смерти не может быть раньше даты рождения!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        dpDeathDate.Focus();
                        return;
                    }
                }

                // Валидация родственных связей
                if (!ValidateRelationships())
                {
                    return;
                }

                using (var context = new GenealogyDBEntities())
                {
                    Persons person;

                    if (editPersonId.HasValue)
                    {
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
                        person = new Persons
                        {
                            TreeId = currentTreeId,
                            CreatedByUserId = Session.UserId,
                            CreatedAt = DateTime.Now
                        };
                        context.Persons.Add(person);
                    }

                    person.LastName = txtLastName.Text.Trim();
                    person.FirstName = txtFirstName.Text.Trim();
                    person.Patronymic = string.IsNullOrWhiteSpace(txtPatronymic.Text) ? null : txtPatronymic.Text.Trim();
                    person.MaidenName = string.IsNullOrWhiteSpace(txtMaidenName.Text) ? null : txtMaidenName.Text.Trim();

                    if (cmbGender.SelectedIndex == 0)
                        person.GenderId = 1;
                    else if (cmbGender.SelectedIndex == 1)
                        person.GenderId = 2;
                    else
                        person.GenderId = 3;

                    person.BirthDate = dpBirthDate.SelectedDate;
                    person.DeathDate = dpDeathDate.SelectedDate;
                    person.BirthPlace = string.IsNullOrWhiteSpace(txtBirthPlace.Text) ? null : txtBirthPlace.Text.Trim();
                    person.DeathPlace = string.IsNullOrWhiteSpace(txtDeathPlace.Text) ? null : txtDeathPlace.Text.Trim();
                    person.Biography = string.IsNullOrWhiteSpace(txtBiography.Text) ? null : txtBiography.Text.Trim();

                    // Обработка фото
                    if (selectedPhotoPath != null && selectedPhotoPath != originalPhotoPath)
                    {
                        // Если выбрано новое фото, копируем его в папку приложения
                        string appFolder = AppDomain.CurrentDomain.BaseDirectory;
                        string photosFolder = System.IO.Path.Combine(appFolder, "Photos");
                        if (!Directory.Exists(photosFolder))
                            Directory.CreateDirectory(photosFolder);

                        string fileName = $"{Guid.NewGuid()}_{System.IO.Path.GetFileName(selectedPhotoPath)}";
                        string destPath = System.IO.Path.Combine(photosFolder, fileName);
                        File.Copy(selectedPhotoPath, destPath, true);
                        person.ProfilePhotoPath = destPath;
                    }
                    else if (selectedPhotoPath == null && originalPhotoPath != null)
                    {
                        // Если фото удалено
                        person.ProfilePhotoPath = null;
                    }
                    else if (selectedPhotoPath == originalPhotoPath)
                    {
                        // Фото не изменилось
                        person.ProfilePhotoPath = originalPhotoPath;
                    }

                    person.UpdatedAt = DateTime.Now;
                    context.SaveChanges();

                    SaveRelationships(context, person.Id);

                    string message = editPersonId.HasValue ? "Изменения сохранены!" : "Персона добавлена!";
                    MessageBox.Show(message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                // Удаляем старые связи
                if (editPersonId.HasValue)
                {
                    var oldRelations = context.Relationships
                        .Where(r => r.Person1Id == personId || r.Person2Id == personId)
                        .ToList();
                    context.Relationships.RemoveRange(oldRelations);
                    context.SaveChanges();
                }

                int fatherId = cmbFather.SelectedValue != null ? (int)cmbFather.SelectedValue : 0;
                int motherId = cmbMother.SelectedValue != null ? (int)cmbMother.SelectedValue : 0;
                int spouseId = cmbSpouse.SelectedValue != null ? (int)cmbSpouse.SelectedValue : 0;

                // Добавляем отца
                if (fatherId > 0)
                {
                    context.Relationships.Add(new Relationships
                    {
                        Person1Id = fatherId,
                        Person2Id = personId,
                        RelationshipType = 1,
                        Direction = 1,
                        CreatedByUserId = Session.UserId,
                        CreatedAt = DateTime.Now
                    });
                }

                // Добавляем мать
                if (motherId > 0)
                {
                    context.Relationships.Add(new Relationships
                    {
                        Person1Id = motherId,
                        Person2Id = personId,
                        RelationshipType = 1,
                        Direction = 1,
                        CreatedByUserId = Session.UserId,
                        CreatedAt = DateTime.Now
                    });
                }

                // Добавляем супруга(у)
                if (spouseId > 0)
                {
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