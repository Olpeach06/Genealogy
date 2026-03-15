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
using System.Windows.Shapes;

namespace Genealogy.Pages
{
    public partial class TreeEditDialog : Window
    {
        public string TreeName { get; private set; }
        public string TreeDescription { get; private set; }
        public bool IsPublic { get; private set; }
        private bool isEditMode = false;

        public TreeEditDialog()
        {
            InitializeComponent();
            txtTitle.Text = "СОЗДАНИЕ ДЕРЕВА";
            chkIsPublic.IsChecked = false;
        }

        public TreeEditDialog(FamilyTrees tree) : this()
        {
            isEditMode = true;
            txtTitle.Text = "РЕДАКТИРОВАНИЕ ДЕРЕВА";
            txtName.Text = tree.Name;
            txtDescription.Text = tree.Description;
            chkIsPublic.IsChecked = tree.IsPublic;
            ValidateFields(null, null);
        }

        private void ValidateFields(object sender, RoutedEventArgs e)
        {
            btnSave.IsEnabled = !string.IsNullOrWhiteSpace(txtName.Text);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Введите название дерева!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TreeName = txtName.Text.Trim();
            TreeDescription = txtDescription.Text?.Trim();
            IsPublic = chkIsPublic.IsChecked ?? false;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}