using Genealogy.AppData;
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
using System.Text.RegularExpressions;

namespace Genealogy.Pages
{
    public partial class LoginPage : Page
    {
        private bool _isProcessing = false;

        public LoginPage()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            PerformLogin();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                PerformLogin();
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                PerformLogin();
            }
        }

        private void PerformLogin()
        {
            if (_isProcessing)
                return;

            try
            {
                _isProcessing = true;

                string login = txtLogin.Text.Trim();
                string password = txtPassword.Password;

                if (string.IsNullOrEmpty(login))
                {
                    MessageBox.Show("Введите логин или email!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtLogin.Focus();
                    return;
                }

                if (string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Введите пароль!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtPassword.Focus();
                    return;
                }

                using (var context = new GenealogyDBEntities())
                {
                    Users user = null;

                    // Проверяем, является ли введенное значение email'ом
                    if (IsValidEmail(login))
                    {
                        // Если это email, ищем по email с регистрозависимым сравнением
                        // Получаем всех пользователей с таким email (без учета регистра)
                        var possibleUsers = context.Users
                            .Where(u => u.Email != null && u.Email.ToLower() == login.ToLower())
                            .ToList();

                        // Затем проверяем точное совпадение с учетом регистра
                        user = possibleUsers.FirstOrDefault(u => u.Email == login);
                    }
                    else
                    {
                        // Если это логин, ищем по логину с регистрозависимым сравнением
                        // Получаем всех пользователей с таким логином (без учета регистра)
                        var possibleUsers = context.Users
                            .Where(u => u.Username.ToLower() == login.ToLower())
                            .ToList();

                        // Затем проверяем точное совпадение с учетом регистра
                        user = possibleUsers.FirstOrDefault(u => u.Username == login);
                    }

                    if (user == null)
                    {
                        MessageBox.Show("Неверный логин/email или пароль!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        txtLogin.Focus();
                        txtLogin.SelectAll();
                        return;
                    }

                    // Проверяем пароль (с учетом регистра)
                    if (user.PasswordHash != password)
                    {
                        MessageBox.Show("Неверный логин/email или пароль!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        txtPassword.Focus();
                        txtPassword.Password = "";
                        return;
                    }

                    if (user.IsActive == false)
                    {
                        MessageBox.Show("Аккаунт заблокирован! Обратитесь к администратору.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Сохраняем в сессию
                    Session.UserId = user.Id;
                    Session.Username = user.Username;
                    Session.RoleId = user.RoleId;
                    Session.IsGuest = false;

                    MessageBox.Show($"Добро пожаловать, {user.Username}!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    NavigationService.Navigate(new MainPage());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Базовая проверка формата email (такая же как на странице регистрации)
                string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
                if (!Regex.IsMatch(email, pattern))
                    return false;

                // Проверка на наличие допустимых доменов
                string[] validDomains = { ".ru", ".com", ".net", ".org", ".info", ".biz", ".gov", ".edu", ".рф" };
                string domain = email.Substring(email.LastIndexOf('.'));

                return validDomains.Any(d => domain.Equals(d, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private void GuestLoginButton_Click(object sender, RoutedEventArgs e)
        {
            Session.IsGuest = true;
            Session.Username = "Гость";
            NavigationService.Navigate(new MainPage());
        }

        private void RegisterHyperlink_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new RegisterPage());
        }
    }
}