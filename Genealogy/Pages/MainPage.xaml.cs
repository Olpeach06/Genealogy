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
    public partial class MainPage : Page
    {
        private int currentTreeId = 1;
        private int? selectedPersonId = null;
        private Dictionary<int, Button> personButtons = new Dictionary<int, Button>();
        private double zoomLevel = 1.0;
        private Point lastMousePosition;

        // Для перетаскивания
        private bool isDragging = false;
        private Button draggedButton = null;
        private Point dragStartPoint;
        private bool isCanvasDragging = false;
        private Point canvasDragStart;
        private ScrollViewer parentScrollViewer;

        public MainPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Загружаем данные сессии
            if (Session.IsGuest)
                txtUserName.Text = "Гость";
            else
                txtUserName.Text = Session.Username;

            // Скрываем кнопки для зрителей
            bool canEdit = Session.IsAdmin || Session.IsEditor;
            btnAdd.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnEdit.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnDelete.Visibility = Session.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnPhoto.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnStory.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnSideEdit.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnSideStory.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;

            // Находим ScrollViewer для возможности перетаскивания канваса
            parentScrollViewer = FindVisualChild<ScrollViewer>(this);

            LoadTree();
        }

        private void LoadTree()
        {
            treeCanvas.Children.Clear();
            personButtons.Clear();

            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var persons = context.Persons
                        .Where(p => p.TreeId == currentTreeId)
                        .ToList();

                    if (!persons.Any())
                    {
                        ShowEmptyMessage();
                        return;
                    }

                    var personIds = persons.Select(p => p.Id).ToList();

                    var relationships = context.Relationships
                        .Where(r => personIds.Contains(r.Person1Id) &&
                                   personIds.Contains(r.Person2Id))
                        .ToList();

                    var positions = CalculatePositions(persons, relationships);

                    DrawRelationships(relationships, positions);
                    DrawPersonButtons(persons, positions);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private Dictionary<int, Point> CalculatePositions(List<Persons> persons, List<Relationships> relationships)
        {
            var positions = new Dictionary<int, Point>();

            // Находим корневые элементы (у кого нет родителей)
            var rootIds = FindRootPersons(persons, relationships);

            // Рассчитываем поколения на основе связей
            var generationLevels = CalculateGenerationLevels(persons, relationships, rootIds);

            // Группируем по поколениям (чем меньше уровень, тем старше)
            var generations = persons
                .GroupBy(p => generationLevels.ContainsKey(p.Id) ? generationLevels[p.Id] : 0)
                .OrderBy(g => g.Key) // 0 - самое старшее поколение (сверху)
                .ToList();

            int startY = 100;
            int yOffset = 180; // Увеличил отступ для лучшей читаемости

            foreach (var gen in generations)
            {
                var genList = gen.ToList();
                int count = genList.Count;

                // Вычисляем начальную позицию X для центрирования
                int startX = Math.Max(200, 1000 - (count * 80)); // Центрируем относительно canvas

                // Сортируем внутри поколения для более логичного расположения
                genList = SortPersonsInGeneration(genList, relationships);

                for (int i = 0; i < count; i++)
                {
                    // Равномерно распределяем по горизонтали
                    positions[genList[i].Id] = new Point(startX + i * 160, startY);
                }
                startY += yOffset;
            }

            return positions;
        }

        private List<int> FindRootPersons(List<Persons> persons, List<Relationships> relationships)
        {
            var personIds = persons.Select(p => p.Id).ToHashSet();
            var childrenIds = new HashSet<int>();

            // Находим всех, кто является ребенком (Person2 в отношениях родитель-ребенок)
            var childIds = relationships
                .Where(r => r.RelationshipType == 1 && personIds.Contains(r.Person2Id))
                .Select(r => r.Person2Id)
                .ToList();

            childrenIds.UnionWith(childIds);

            // Корневые - те, кто не является ребенком
            return personIds.Where(id => !childrenIds.Contains(id)).ToList();
        }

        private Dictionary<int, int> CalculateGenerationLevels(List<Persons> persons,
            List<Relationships> relationships, List<int> rootIds)
        {
            var levels = new Dictionary<int, int>();
            var queue = new Queue<(int personId, int level)>();

            // Инициализируем корневые элементы уровнем 0
            foreach (var rootId in rootIds)
            {
                levels[rootId] = 0;
                queue.Enqueue((rootId, 0));
            }

            while (queue.Count > 0)
            {
                var (currentId, currentLevel) = queue.Dequeue();

                // Находим всех детей текущей персоны
                var children = relationships
                    .Where(r => r.RelationshipType == 1 && r.Person1Id == currentId)
                    .Select(r => r.Person2Id)
                    .ToList();

                foreach (var childId in children)
                {
                    if (!levels.ContainsKey(childId))
                    {
                        levels[childId] = currentLevel + 1;
                        queue.Enqueue((childId, currentLevel + 1));
                    }
                }
            }

            // Для персон, к которым не удалось добраться через связи
            foreach (var person in persons)
            {
                if (!levels.ContainsKey(person.Id))
                {
                    // Пытаемся определить уровень по году рождения
                    if (person.BirthDate != null)
                    {
                        int year = person.BirthDate.Value.Year;
                        if (year < 1950) levels[person.Id] = 0;
                        else if (year < 1980) levels[person.Id] = 1;
                        else if (year < 2000) levels[person.Id] = 2;
                        else levels[person.Id] = 3;
                    }
                    else
                    {
                        levels[person.Id] = 1; // Средний уровень по умолчанию
                    }
                }
            }

            return levels;
        }

        private List<Persons> SortPersonsInGeneration(List<Persons> persons, List<Relationships> relationships)
        {
            // Сортируем по возрасту или другим критериям
            return persons.OrderBy(p => p.BirthDate).ToList();
        }

        private void DrawRelationships(List<Relationships> relationships, Dictionary<int, Point> positions)
        {
            foreach (var rel in relationships)
            {
                if (!positions.ContainsKey(rel.Person1Id) || !positions.ContainsKey(rel.Person2Id))
                    continue;

                Point p1 = positions[rel.Person1Id];
                Point p2 = positions[rel.Person2Id];

                // Корректируем точки для соединения от центра кнопок
                p1.Offset(60, 30); // Смещение к центру кнопки (120/2 = 60, 60/2 = 30)
                p2.Offset(60, 30);

                var line = new Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = GetLineColor(rel.RelationshipType),
                    StrokeThickness = rel.RelationshipType == 2 ? 3 : 2,
                    Opacity = 0.7
                };

                // Добавляем стрелку для указания направления (родитель -> ребенок)
                if (rel.RelationshipType == 1)
                {
                    line.StrokeDashArray = new DoubleCollection { 1 }; // Пунктир для родительских связей
                }

                Canvas.SetZIndex(line, 0);
                treeCanvas.Children.Add(line);
            }
        }

        private Brush GetLineColor(int type)
        {
            if (type == 1) // Родитель-ребенок
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C4E3D"));
            else if (type == 2) // Супруги
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8FAA7A"));
            else if (type == 3) // Сиблинги
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7A48B"));
            else
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B7E6B"));
        }

        private void DrawPersonButtons(List<Persons> persons, Dictionary<int, Point> positions)
        {
            foreach (var person in persons)
            {
                if (!positions.ContainsKey(person.Id)) continue;

                Point pos = positions[person.Id];

                string genderSymbol = "👤";
                if (person.GenderId == 1) genderSymbol = "♂";
                else if (person.GenderId == 2) genderSymbol = "♀";

                string birthYear = person.BirthDate?.Year.ToString() ?? "?";
                string deathYear = person.DeathDate?.Year.ToString() ?? "";

                string dateInfo = birthYear;
                if (!string.IsNullOrEmpty(deathYear))
                    dateInfo += $" - {deathYear}";

                var btn = new Button
                {
                    Content = $"{person.FirstName} {person.LastName}\n{genderSymbol} {dateInfo}",
                    Width = 120,
                    Height = 60,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDF8F0")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7A48B")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C4E3D")),
                    FontWeight = FontWeights.SemiBold,
                    Padding = new Thickness(5),
                    Tag = person.Id,
                    ToolTip = $"{person.LastName} {person.FirstName} {person.Patronymic}\n" +
                             $"Родился: {person.BirthDate?.ToString("dd.MM.yyyy") ?? "неизвестно"}"
                };

                btn.Click += PersonButton_Click;
                btn.MouseRightButtonDown += PersonButton_RightClick;

                // Добавляем обработчики для перетаскивания
                btn.MouseLeftButtonDown += PersonButton_MouseLeftButtonDown;
                btn.MouseLeftButtonUp += PersonButton_MouseLeftButtonUp;
                btn.MouseMove += PersonButton_MouseMove;

                Canvas.SetLeft(btn, pos.X);
                Canvas.SetTop(btn, pos.Y);
                Canvas.SetZIndex(btn, 1);

                treeCanvas.Children.Add(btn);
                personButtons[person.Id] = btn;
            }
        }

        // Обработчики перетаскивания персон
        private void PersonButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(Session.IsAdmin || Session.IsEditor))
            {
                e.Handled = true;
                return;
            }

            var btn = sender as Button;
            if (btn == null) return;

            isDragging = true;
            draggedButton = btn;
            dragStartPoint = e.GetPosition(treeCanvas);

            // Поднимаем кнопку выше других во время перетаскивания
            Canvas.SetZIndex(draggedButton, 10);

            // Захватываем мышь, чтобы получать события даже за пределами кнопки
            btn.CaptureMouse();

            e.Handled = true;
        }

        private void PersonButton_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || draggedButton == null) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                isDragging = false;
                draggedButton.ReleaseMouseCapture();
                Canvas.SetZIndex(draggedButton, 1);
                draggedButton = null;
                return;
            }

            Point currentPos = e.GetPosition(treeCanvas);

            // Вычисляем смещение
            double offsetX = currentPos.X - dragStartPoint.X;
            double offsetY = currentPos.Y - dragStartPoint.Y;

            // Получаем текущие координаты кнопки
            double newLeft = Canvas.GetLeft(draggedButton) + offsetX;
            double newTop = Canvas.GetTop(draggedButton) + offsetY;

            // Ограничиваем перемещение в пределах canvas
            newLeft = Math.Max(0, Math.Min(treeCanvas.Width - draggedButton.Width, newLeft));
            newTop = Math.Max(0, Math.Min(treeCanvas.Height - draggedButton.Height, newTop));

            // Перемещаем кнопку
            Canvas.SetLeft(draggedButton, newLeft);
            Canvas.SetTop(draggedButton, newTop);

            // Обновляем начальную позицию для следующего шага
            dragStartPoint = currentPos;

            e.Handled = true;
        }

        private void PersonButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDragging || draggedButton == null) return;

            // Сохраняем новую позицию (здесь можно добавить сохранение в БД)
            if (draggedButton.Tag is int personId)
            {
                // TODO: Сохранить новую позицию в базу данных
                // SavePersonPosition(personId, Canvas.GetLeft(draggedButton), Canvas.GetTop(draggedButton));
                MessageBox.Show($"Персона перемещена. Новые координаты: X={Canvas.GetLeft(draggedButton):F0}, Y={Canvas.GetTop(draggedButton):F0}",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // Возвращаем нормальный Z-индекс
            Canvas.SetZIndex(draggedButton, 1);

            // Завершаем перетаскивание
            isDragging = false;
            draggedButton.ReleaseMouseCapture();
            draggedButton = null;

            e.Handled = true;
        }

        // Обработчики для перетаскивания всего canvas
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Если клик не по кнопке, начинаем перетаскивание canvas
            if (e.OriginalSource == treeCanvas)
            {
                isCanvasDragging = true;
                canvasDragStart = e.GetPosition(this);
                treeCanvas.CaptureMouse();
                Mouse.OverrideCursor = Cursors.ScrollAll;
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isCanvasDragging && parentScrollViewer != null)
            {
                Point currentPos = e.GetPosition(this);
                Vector diff = currentPos - canvasDragStart;

                // Прокручиваем ScrollViewer
                parentScrollViewer.ScrollToHorizontalOffset(parentScrollViewer.HorizontalOffset - diff.X);
                parentScrollViewer.ScrollToVerticalOffset(parentScrollViewer.VerticalOffset - diff.Y);

                canvasDragStart = currentPos;
                e.Handled = true;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isCanvasDragging)
            {
                isCanvasDragging = false;
                treeCanvas.ReleaseMouseCapture();
                Mouse.OverrideCursor = null;
                e.Handled = true;
            }
        }

        // Вспомогательный метод для поиска ScrollViewer
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    var descendant = FindVisualChild<T>(child);
                    if (descendant != null)
                        return descendant;
                }
            }
            return null;
        }

        private void PersonButton_Click(object sender, RoutedEventArgs e)
        {
            // Если это было перетаскивание, не обрабатываем клик
            if (isDragging) return;

            var btn = sender as Button;
            if (btn?.Tag == null) return;

            int personId = (int)btn.Tag;
            selectedPersonId = personId;

            // Сброс цвета всех кнопок
            foreach (var button in personButtons.Values)
            {
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDF8F0"));
            }

            // Подсветка выбранной
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AFC09A"));

            ShowPersonDetails(personId);
        }

        private void PersonButton_RightClick(object sender, MouseButtonEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag == null) return;

            int personId = (int)btn.Tag;
            selectedPersonId = personId;

            var contextMenu = new ContextMenu();

            var editItem = new MenuItem { Header = "Редактировать" };
            editItem.Click += (s, args) => EditPerson(personId);

            var storyItem = new MenuItem { Header = "Добавить историю" };
            storyItem.Click += (s, args) => AddStory(personId);

            var photoItem = new MenuItem { Header = "Добавить фото" };
            photoItem.Click += (s, args) => AddPhoto(personId);

            contextMenu.Items.Add(editItem);
            contextMenu.Items.Add(storyItem);
            contextMenu.Items.Add(photoItem);

            if (Session.IsAdmin)
            {
                contextMenu.Items.Add(new Separator());
                var deleteItem = new MenuItem { Header = "Удалить" };
                deleteItem.Click += (s, args) => DeletePerson(personId);
                contextMenu.Items.Add(deleteItem);
            }

            btn.ContextMenu = contextMenu;
            contextMenu.IsOpen = true;
        }

        private void ShowPersonDetails(int personId)
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var person = context.Persons.FirstOrDefault(p => p.Id == personId);
                    if (person == null) return;

                    txtPersonName.Text = $"{person.LastName} {person.FirstName} {person.Patronymic}".Trim();
                    txtBirthDate.Text = person.BirthDate?.ToString("dd.MM.yyyy") ?? "?";
                    txtDeathDate.Text = person.DeathDate?.ToString("dd.MM.yyyy") ?? "...";

                    var gender = context.Genders.FirstOrDefault(g => g.Id == person.GenderId);
                    if (gender != null)
                    {
                        txtGender.Text = gender.Name;
                        txtGenderSymbol.Text = gender.Symbol ?? "👤";
                    }

                    // Родители
                    var parentIds = context.Relationships
                        .Where(r => r.Person2Id == personId && r.RelationshipType == 1)
                        .Select(r => r.Person1Id).ToList();

                    if (parentIds.Any())
                    {
                        var parents = context.Persons
                            .Where(p => parentIds.Contains(p.Id))
                            .Select(p => $"{p.FirstName} {p.LastName}")
                            .ToList();
                        txtParents.Text = $"Родители: {string.Join(", ", parents)}";
                    }
                    else txtParents.Text = "Родители: нет данных";

                    // Супруг
                    var spouseRel = context.Relationships
                        .FirstOrDefault(r => (r.Person1Id == personId || r.Person2Id == personId)
                                           && r.RelationshipType == 2);

                    if (spouseRel != null)
                    {
                        int spouseId = spouseRel.Person1Id == personId ? spouseRel.Person2Id : spouseRel.Person1Id;
                        var spouse = context.Persons.FirstOrDefault(p => p.Id == spouseId);
                        txtSpouse.Text = spouse != null
                            ? $"Супруг(а): {spouse.FirstName} {spouse.LastName}"
                            : "Супруг(а): нет";
                    }
                    else txtSpouse.Text = "Супруг(а): нет";

                    // Дети
                    var childIds = context.Relationships
                        .Where(r => r.Person1Id == personId && r.RelationshipType == 1)
                        .Select(r => r.Person2Id).ToList();

                    if (childIds.Any())
                    {
                        var children = context.Persons
                            .Where(p => childIds.Contains(p.Id))
                            .Select(p => $"{p.FirstName} {p.LastName}")
                            .ToList();
                        txtChildren.Text = $"Дети: {string.Join(", ", children)}";
                    }
                    else txtChildren.Text = "Дети: нет";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void ShowEmptyMessage()
        {
            var tb = new TextBlock
            {
                Text = "Древо пусто. Нажмите ➕ Добавить чтобы начать",
                FontSize = 18,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B7E6B")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(tb, 400);
            Canvas.SetTop(tb, 300);
            treeCanvas.Children.Add(tb);
        }

        // Обработчики кнопок
        private void MyTreesButton_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Переход к списку деревьев");

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtSearch.Text) && txtSearch.Text != "Поиск...")
                MessageBox.Show($"Поиск: {txtSearch.Text}");
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
            => MessageBox.Show("Переход к отчетам");

        private void AddPersonButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new EditPersonPage());
        }

        private void EditPersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPersonId.HasValue)
                EditPerson(selectedPersonId.Value);
            else
                MessageBox.Show("Выберите персону");
        }

        private void DeletePersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPersonId.HasValue)
                DeletePerson(selectedPersonId.Value);
            else
                MessageBox.Show("Выберите персону");
        }

        private void AddPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPersonId.HasValue)
                AddPhoto(selectedPersonId.Value);
            else
                MessageBox.Show("Выберите персону");
        }

        private void AddStoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPersonId.HasValue)
                AddStory(selectedPersonId.Value);
            else
                MessageBox.Show("Выберите персону");
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPersonId.HasValue)
                NavigationService.Navigate(new PersonProfilePage(selectedPersonId.Value));
            else
                MessageBox.Show("Выберите персону");
        }

        // Вспомогательные методы
        private void EditPerson(int id) => MessageBox.Show($"Редактирование персоны ID: {id}");
        private void DeletePerson(int id) => MessageBox.Show($"Удаление персоны ID: {id}");
        private void AddPhoto(int id) => MessageBox.Show($"Добавление фото для ID: {id}");
        private void AddStory(int id) => MessageBox.Show($"Добавление истории для ID: {id}");

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Здесь будет фильтрация
        }

        private void txtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSearch.Text == "Поиск...")
                txtSearch.Text = "";
        }

        private void txtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
                txtSearch.Text = "Поиск...";
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            Session.Clear();
            NavigationService.Navigate(new LoginPage());
        }
    }
}