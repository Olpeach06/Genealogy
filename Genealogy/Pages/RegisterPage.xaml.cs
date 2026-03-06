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
    public partial class RegisterPage : Page
    {
        public RegisterPage()
        {
            InitializeComponent();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string login = txtLogin.Text.Trim();
                string email = txtEmail.Text.Trim();
                string password = txtPassword.Password;
                string confirm = txtConfirmPassword.Password;

                // Проверки
                if (string.IsNullOrEmpty(login))
                {
                    MessageBox.Show("Введите логин!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(password))
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

                using (var context = new GenealogyDBEntities())
                {
                    // Проверка уникальности логина
                    var existingUser = context.Users.FirstOrDefault(u => u.Username == login);
                    if (existingUser != null)
                    {
                        MessageBox.Show("Пользователь с таким логином уже существует!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Проверка email
                    if (!string.IsNullOrEmpty(email))
                    {
                        var existingEmail = context.Users.FirstOrDefault(u => u.Email == email);
                        if (existingEmail != null)
                        {
                            MessageBox.Show("Этот email уже используется!", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    // Определение роли
                    int roleId = 3; // Зритель
                    bool isFirstUser = !context.Users.Any();
                    if (isFirstUser)
                    {
                        roleId = 1; // Администратор
                    }

                    // Создание пользователя
                    var user = new Users
                    {
                        Username = login,
                        Email = string.IsNullOrEmpty(email) ? null : email,
                        PasswordHash = password, // В реальном проекте нужно хэшировать!
                        RoleId = roleId,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };

                    context.Users.Add(user);
                    context.SaveChanges();

                    string message = isFirstUser
                        ? "Регистрация успешна! Вы администратор."
                        : "Регистрация успешна!";

                    MessageBox.Show(message, "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // Переход на страницу входа
                    NavigationService.Navigate(new LoginPage());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoginHyperlink_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new LoginPage());
        }
    }
}