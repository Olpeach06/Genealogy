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
using Genealogy.Classes;
using Genealogy.AppData;

namespace Genealogy.Pages
{
    public partial class EditUserPage : Page
    {
        private int? userId = null;
        private bool isEditMode = false;

        public class RoleItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public EditUserPage()
        {
            InitializeComponent();
            isEditMode = false;
            txtTitle.Text = "ДОБАВЛЕНИЕ ПОЛЬЗОВАТЕЛЯ";
            txtConfirmLabel.Text = "Подтверждение пароля *";
        }

        public EditUserPage(int userId) : this()
        {
            this.userId = userId;
            isEditMode = true;
            txtTitle.Text = "РЕДАКТИРОВАНИЕ ПОЛЬЗОВАТЕЛЯ";
            txtConfirmLabel.Visibility = Visibility.Collapsed;
            txtConfirmPassword.Visibility = Visibility.Collapsed;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRoles();

            if (isEditMode && userId.HasValue)
            {
                LoadUserData();
            }
        }

        private void LoadRoles()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var roles = context.UserRoles
                        .Select(r => new RoleItem
                        {
                            Id = r.Id,
                            Name = r.Name
                        })
                        .ToList();

                    cmbRole.ItemsSource = roles;
                    cmbRole.SelectedValuePath = "Id";
                    cmbRole.DisplayMemberPath = "Name";
                    cmbRole.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки ролей: {ex.Message}");
            }
        }

        private void LoadUserData()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var user = context.Users.Find(userId);
                    if (user == null)
                    {
                        MessageBox.Show("Пользователь не найден!");
                        NavigationService.GoBack();
                        return;
                    }

                    txtUsername.Text = user.Username;
                    txtEmail.Text = user.Email;
                    cmbRole.SelectedValue = user.RoleId;
                    chkIsActive.IsChecked = user.IsActive;

                    // Пароль не показываем при редактировании
                    txtPassword.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = txtUsername.Text.Trim();
                string email = txtEmail.Text.Trim();

                if (string.IsNullOrWhiteSpace(username))
                {
                    MessageBox.Show("Введите логин!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (var context = new GenealogyDBEntities())
                {
                    Users user;

                    if (isEditMode && userId.HasValue)
                    {
                        // Редактирование
                        user = context.Users.Find(userId);
                        if (user == null)
                        {
                            MessageBox.Show("Пользователь не найден!");
                            return;
                        }

                        user.Username = username;
                        user.Email = string.IsNullOrWhiteSpace(email) ? null : email;
                        user.RoleId = (int)cmbRole.SelectedValue;
                        user.IsActive = chkIsActive.IsChecked ?? true;

                        context.SaveChanges();

                        MessageBox.Show("Данные пользователя обновлены!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        // Добавление
                        string password = txtPassword.Password;
                        string confirm = txtConfirmPassword.Password;

                        if (string.IsNullOrWhiteSpace(password))
                        {
                            MessageBox.Show("Введите пароль!", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        if (password != confirm)
                        {
                            MessageBox.Show("Пароли не совпадают!", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        if (password.Length < 4)
                        {
                            MessageBox.Show("Пароль должен быть не менее 4 символов!", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Проверка уникальности логина
                        var existing = context.Users.FirstOrDefault(u => u.Username == username);
                        if (existing != null)
                        {
                            MessageBox.Show("Пользователь с таким логином уже существует!", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        user = new Users
                        {
                            Username = username,
                            PasswordHash = password, // В реальном проекте нужно хэшировать!
                            Email = string.IsNullOrWhiteSpace(email) ? null : email,
                            RoleId = (int)cmbRole.SelectedValue,
                            IsActive = chkIsActive.IsChecked ?? true,
                            CreatedAt = DateTime.Now
                        };

                        context.Users.Add(user);
                        context.SaveChanges();

                        MessageBox.Show("Пользователь добавлен!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    NavigationService.GoBack();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}