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
    public partial class LoginPage : Page
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string login = txtLogin.Text;
                string password = txtPassword.Password;

                if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Введите логин и пароль!", "Ошибка авторизации",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Здесь будет проверка в БД
                // Пока для теста:
                if (login == "admin" && password == "admin123")
                {
                    MessageBox.Show("Добро пожаловать, Администратор!", "Успешный вход",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    NavigationService.Navigate(new MainPage());
                }
                else if (login == "editor" && password == "editor123")
                {
                    MessageBox.Show("Добро пожаловать, Редактор!", "Успешный вход",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    NavigationService.Navigate(new MainPage());
                }
                else if (login == "viewer" && password == "viewer123")
                {
                    MessageBox.Show("Добро пожаловать, Зритель!", "Успешный вход",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    NavigationService.Navigate(new MainPage());
                }
                else
                {
                    MessageBox.Show("Неверный логин или пароль!", "Ошибка авторизации",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при авторизации: {ex.Message}",
                    "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GuestLoginButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вход как гость (только просмотр публичных деревьев)",
                "Режим гостя", MessageBoxButton.OK, MessageBoxImage.Information);
            NavigationService.Navigate(new MainPage());
        }

        private void RegisterHyperlink_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new RegisterPage());
        }
    }
}