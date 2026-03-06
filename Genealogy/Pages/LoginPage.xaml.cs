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
                string login = txtLogin.Text.Trim();
                string password = txtPassword.Password;

                if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Введите логин и пароль!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (var context = new GenealogyDBEntities())
                {
                    var user = context.Users
                        .FirstOrDefault(u => u.Username == login &&
                                             u.PasswordHash == password);

                    if (user == null)
                    {
                        MessageBox.Show("Неверный логин или пароль!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (user.IsActive == false)
                    {
                        MessageBox.Show("Аккаунт заблокирован!", "Ошибка",
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