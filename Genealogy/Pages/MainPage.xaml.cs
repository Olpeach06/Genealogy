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

        // Для перетаскивания
        private bool isDragging = false;
        private Button draggedButton = null;
        private Point dragStartPoint;
        private bool isCanvasDragging = false;
        private Point canvasDragStart;
        private ScrollViewer parentScrollViewer;

        // Для поиска
        private string currentSearchText = "";
        private List<Persons> allPersons = new List<Persons>();
        private Dictionary<int, Point> currentPositions = new Dictionary<int, Point>();
        private System.Windows.Threading.DispatcherTimer searchNotificationTimer;

        // Для фильтрации
        private Dictionary<int, int> personGenerations = new Dictionary<int, int>();
        private List<Relationships> allRelationships = new List<Relationships>();
        private int? currentGenerationFilter = null;

        // Для группировки семей
        private Dictionary<int, List<int>> families = new Dictionary<int, List<int>>();
        private Dictionary<int, int> spousePairs = new Dictionary<int, int>();

        // Флаг, указывающий, что страница полностью загружена
        private bool isPageLoaded = false;

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
            btnSideEdit.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnSideStory.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;

            // Находим ScrollViewer для возможности перетаскивания канваса
            parentScrollViewer = FindVisualChild<ScrollViewer>(this);

            LoadTree();

            // Устанавливаем флаг, что страница загружена
            isPageLoaded = true;
        }

        private void LoadTree()
        {
            if (treeCanvas == null) return;

            treeCanvas.Children.Clear();
            personButtons.Clear();
            allPersons.Clear();
            allRelationships.Clear();
            families.Clear();
            spousePairs.Clear();
            HideNoResultsNotification();

            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    allPersons = context.Persons
                        .Where(p => p.TreeId == currentTreeId)
                        .ToList();

                    if (!allPersons.Any())
                    {
                        ShowEmptyMessage();
                        return;
                    }

                    var personIds = allPersons.Select(p => p.Id).ToList();

                    allRelationships = context.Relationships
                        .Where(r => personIds.Contains(r.Person1Id) &&
                                   personIds.Contains(r.Person2Id))
                        .ToList();

                    // Определяем супружеские пары
                    FindSpousePairs();

                    // Определяем семьи (группируем по родителям)
                    FindFamilies();

                    currentPositions = CalculatePositions(allPersons, allRelationships);

                    DrawRelationships(allRelationships, currentPositions);
                    DrawPersonButtons(allPersons, currentPositions);

                    if (currentGenerationFilter.HasValue)
                    {
                        ApplyGenerationFilter(currentGenerationFilter.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private void FindSpousePairs()
        {
            foreach (var rel in allRelationships.Where(r => r.RelationshipType == 2))
            {
                spousePairs[rel.Person1Id] = rel.Person2Id;
                spousePairs[rel.Person2Id] = rel.Person1Id;
            }
        }

        private void FindFamilies()
        {
            // Группируем детей по парам родителей
            var parentChildGroups = allRelationships
                .Where(r => r.RelationshipType == 1)
                .GroupBy(r => r.Person1Id)
                .ToDictionary(g => g.Key, g => g.Select(r => r.Person2Id).ToList());

            foreach (var parent in parentChildGroups)
            {
                // Если у родителя есть супруг(а), объединяем их детей
                if (spousePairs.ContainsKey(parent.Key))
                {
                    int spouseId = spousePairs[parent.Key];
                    if (parentChildGroups.ContainsKey(spouseId))
                    {
                        var allChildren = parent.Value.Union(parentChildGroups[spouseId]).Distinct().ToList();
                        families[parent.Key] = allChildren;
                        families[spouseId] = allChildren;
                    }
                    else
                    {
                        families[parent.Key] = parent.Value;
                    }
                }
                else
                {
                    families[parent.Key] = parent.Value;
                }
            }
        }

        private Dictionary<int, Point> CalculatePositions(List<Persons> persons, List<Relationships> relationships)
        {
            var positions = new Dictionary<int, Point>();

            // Находим корневые персоны (у которых нет родителей)
            var rootIds = FindRootPersons(persons, relationships);

            // Рассчитываем поколения
            var generationLevels = CalculateGenerationLevels(persons, relationships, rootIds);

            // Группируем по поколениям
            var generations = persons
                .GroupBy(p => generationLevels.ContainsKey(p.Id) ? generationLevels[p.Id] : 0)
                .OrderBy(g => g.Key)
                .ToList();

            int startY = 100;
            int yOffset = 180;

            // Для каждого поколения
            foreach (var gen in generations)
            {
                var genList = gen.ToList();

                // Сортируем внутри поколения: сначала группируем по семьям
                var familiesInGen = GroupByFamilies(genList);

                int currentX = 200;
                int spacing = 200; // Расстояние между семьями

                foreach (var family in familiesInGen)
                {
                    if (family.Count == 2) // Супружеская пара
                    {
                        // Сортируем супругов по возрасту (старший слева)
                        var sortedCouple = family.OrderBy(p => p.BirthDate).ToList();

                        positions[sortedCouple[0].Id] = new Point(currentX, startY);
                        positions[sortedCouple[1].Id] = new Point(currentX + 140, startY);

                        currentX += 280; // Отступ после пары
                    }
                    else if (family.Count == 1) // Одиночная персона
                    {
                        positions[family[0].Id] = new Point(currentX + 70, startY);
                        currentX += 200;
                    }
                }

                startY += yOffset;
            }

            return positions;
        }

        private List<List<Persons>> GroupByFamilies(List<Persons> generation)
        {
            var families = new List<List<Persons>>();
            var usedPersons = new HashSet<int>();

            foreach (var person in generation)
            {
                if (usedPersons.Contains(person.Id)) continue;

                var family = new List<Persons> { person };

                // Проверяем, есть ли у персоны супруг(а) в этом же поколении
                if (spousePairs.ContainsKey(person.Id))
                {
                    int spouseId = spousePairs[person.Id];
                    var spouse = generation.FirstOrDefault(p => p.Id == spouseId);
                    if (spouse != null)
                    {
                        family.Add(spouse);
                        usedPersons.Add(spouseId);
                    }
                }

                families.Add(family);
                usedPersons.Add(person.Id);
            }

            return families;
        }

        private List<int> FindRootPersons(List<Persons> persons, List<Relationships> relationships)
        {
            var personIds = persons.Select(p => p.Id).ToHashSet();
            var childrenIds = new HashSet<int>();

            var childIds = relationships
                .Where(r => r.RelationshipType == 1 && personIds.Contains(r.Person2Id))
                .Select(r => r.Person2Id)
                .ToList();

            childrenIds.UnionWith(childIds);

            // Корневые - те, кто не является ребенком
            var roots = personIds.Where(id => !childrenIds.Contains(id)).ToList();

            // Если нет корневых, берем самых старших по дате рождения
            if (!roots.Any())
            {
                roots = persons.OrderBy(p => p.BirthDate).Take(1).Select(p => p.Id).ToList();
            }

            return roots;
        }

        private Dictionary<int, int> CalculateGenerationLevels(List<Persons> persons,
            List<Relationships> relationships, List<int> rootIds)
        {
            var levels = new Dictionary<int, int>();
            var queue = new Queue<(int personId, int level)>();

            foreach (var rootId in rootIds)
            {
                levels[rootId] = 0;
                queue.Enqueue((rootId, 0));
            }

            while (queue.Count > 0)
            {
                var (currentId, currentLevel) = queue.Dequeue();

                // Ищем детей (где currentId является родителем)
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

            // Для персон, у которых не определен уровень (например, из-за отсутствия связей)
            foreach (var person in persons)
            {
                if (!levels.ContainsKey(person.Id))
                {
                    if (person.BirthDate != null)
                    {
                        // Определяем уровень примерно по году рождения
                        int year = person.BirthDate.Value.Year;
                        if (year < 1950) levels[person.Id] = 0;
                        else if (year < 1980) levels[person.Id] = 1;
                        else if (year < 2000) levels[person.Id] = 2;
                        else levels[person.Id] = 3;
                    }
                    else
                    {
                        // Если нет даты рождения, ставим в самое нижнее поколение
                        levels[person.Id] = 3;
                    }
                }
            }

            personGenerations = levels;
            return levels;
        }

        private void DrawRelationships(List<Relationships> relationships, Dictionary<int, Point> positions)
        {
            if (treeCanvas == null) return;

            foreach (var rel in relationships)
            {
                if (!positions.ContainsKey(rel.Person1Id) || !positions.ContainsKey(rel.Person2Id))
                    continue;

                Point p1 = positions[rel.Person1Id];
                Point p2 = positions[rel.Person2Id];

                // Смещаем к центру кнопок
                p1.Offset(60, 30);
                p2.Offset(60, 30);

                var line = new Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = GetLineColor(rel.RelationshipType),
                    StrokeThickness = rel.RelationshipType == 2 ? 3 : 2,
                    Opacity = 0.7,
                    Tag = $"line_{rel.Person1Id}_{rel.Person2Id}"
                };

                // Для родительских связей рисуем пунктиром
                if (rel.RelationshipType == 1)
                {
                    line.StrokeDashArray = new DoubleCollection { 1 };
                }

                Canvas.SetZIndex(line, 0);
                treeCanvas.Children.Add(line);
            }

            // Рисуем дополнительные связи между супругами, если их нет в relationships
            DrawMissingSpouseLines(positions);
        }

        private void DrawMissingSpouseLines(Dictionary<int, Point> positions)
        {
            foreach (var pair in spousePairs)
            {
                int person1Id = pair.Key;
                int person2Id = pair.Value;

                // Проверяем, есть ли уже линия между супругами
                bool lineExists = treeCanvas.Children.OfType<Line>().Any(l =>
                    (l.Tag?.ToString() == $"line_{person1Id}_{person2Id}" ||
                     l.Tag?.ToString() == $"line_{person2Id}_{person1Id}") &&
                    l.StrokeThickness == 3);

                if (!lineExists && positions.ContainsKey(person1Id) && positions.ContainsKey(person2Id))
                {
                    Point p1 = positions[person1Id];
                    Point p2 = positions[person2Id];

                    p1.Offset(60, 30);
                    p2.Offset(60, 30);

                    var line = new Line
                    {
                        X1 = p1.X,
                        Y1 = p1.Y,
                        X2 = p2.X,
                        Y2 = p2.Y,
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8FAA7A")),
                        StrokeThickness = 3,
                        Opacity = 0.7,
                        Tag = $"line_{person1Id}_{person2Id}"
                    };

                    Canvas.SetZIndex(line, 0);
                    treeCanvas.Children.Add(line);
                }
            }
        }

        private Brush GetLineColor(int type)
        {
            if (type == 1)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C4E3D"));
            else if (type == 2)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8FAA7A"));
            else if (type == 3)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7A48B"));
            else
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B7E6B"));
        }

        private void DrawPersonButtons(List<Persons> persons, Dictionary<int, Point> positions)
        {
            if (treeCanvas == null) return;

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

        // Остальные методы остаются без изменений...
        // (Filter_Changed, ShowAllGenerations, ApplyGenerationFilter, 
        // SearchButton_Click, PerformSearch, и все остальные методы 
        // для поиска, перетаскивания и обработки кликов)

        // ==================== ФИЛЬТРАЦИЯ ПО ПОКОЛЕНИЯМ ====================

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!isPageLoaded || treeCanvas == null || personButtons == null || !personButtons.Any())
            {
                return;
            }

            if (cmbFilter.SelectedItem == null) return;

            string selectedFilter = (cmbFilter.SelectedItem as ComboBoxItem)?.Content.ToString();

            switch (selectedFilter)
            {
                case "Все поколения":
                    currentGenerationFilter = null;
                    ShowAllGenerations();
                    break;
                case "Поколение 1":
                    currentGenerationFilter = 0;
                    ApplyGenerationFilter(0);
                    break;
                case "Поколение 2":
                    currentGenerationFilter = 1;
                    ApplyGenerationFilter(1);
                    break;
                case "Поколение 3":
                    currentGenerationFilter = 2;
                    ApplyGenerationFilter(2);
                    break;
                case "Поколение 4":
                    currentGenerationFilter = 3;
                    ApplyGenerationFilter(3);
                    break;
            }
        }

        private void ShowAllGenerations()
        {
            if (treeCanvas == null) return;

            if (personButtons != null)
            {
                foreach (var btn in personButtons.Values)
                {
                    if (btn != null)
                        btn.Visibility = Visibility.Visible;
                }
            }

            foreach (var child in treeCanvas.Children)
            {
                if (child is Line line)
                {
                    line.Visibility = Visibility.Visible;
                }
            }
        }

        private void ApplyGenerationFilter(int generationLevel)
        {
            if (treeCanvas == null || personGenerations == null || !personGenerations.Any() || personButtons == null)
            {
                return;
            }

            var personIdsInGeneration = personGenerations
                .Where(g => g.Value == generationLevel)
                .Select(g => g.Key)
                .ToHashSet();

            foreach (var btn in personButtons.Values)
            {
                if (btn != null)
                    btn.Visibility = Visibility.Collapsed;
            }

            foreach (var personId in personIdsInGeneration)
            {
                if (personButtons.ContainsKey(personId) && personButtons[personId] != null)
                {
                    personButtons[personId].Visibility = Visibility.Visible;
                }
            }

            foreach (var child in treeCanvas.Children)
            {
                if (child is Line line)
                {
                    line.Visibility = Visibility.Collapsed;
                }
            }

            foreach (var rel in allRelationships)
            {
                bool person1Visible = personIdsInGeneration.Contains(rel.Person1Id);
                bool person2Visible = personIdsInGeneration.Contains(rel.Person2Id);

                if (person1Visible && person2Visible)
                {
                    foreach (var child in treeCanvas.Children)
                    {
                        if (child is Line line &&
                            line.Tag?.ToString() == $"line_{rel.Person1Id}_{rel.Person2Id}")
                        {
                            line.Visibility = Visibility.Visible;
                            break;
                        }
                    }
                }
            }
        }

        // ==================== ПОИСК ====================

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformSearch();
                e.Handled = true;
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (btnClearSearch == null) return;

            if (txtSearch.Text != "Поиск..." && !string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                btnClearSearch.Visibility = Visibility.Visible;
            }
            else
            {
                btnClearSearch.Visibility = Visibility.Collapsed;
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "Поиск...";
            ClearSearchHighlight();
            currentSearchText = "";
            txtSearch.ToolTip = null;
            btnClearSearch.Visibility = Visibility.Collapsed;
            HideNoResultsNotification();
        }

        private void PerformSearch()
        {
            string searchText = txtSearch.Text.Trim();

            if (string.IsNullOrWhiteSpace(searchText) || searchText == "Поиск...")
            {
                ClearSearchHighlight();
                currentSearchText = "";
                txtSearch.ToolTip = null;
                HideNoResultsNotification();
                return;
            }

            currentSearchText = searchText.ToLower();

            var matchingPersons = allPersons.Where(p =>
                (p.LastName?.ToLower().Contains(currentSearchText) ?? false) ||
                (p.FirstName?.ToLower().Contains(currentSearchText) ?? false) ||
                (p.Patronymic?.ToLower().Contains(currentSearchText) ?? false)
            ).ToList();

            foreach (var button in personButtons.Values)
            {
                if (button != null)
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDF8F0"));
                    button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7A48B"));
                    button.BorderThickness = new Thickness(1);
                }
            }

            if (matchingPersons.Any())
            {
                foreach (var person in matchingPersons)
                {
                    if (personButtons.ContainsKey(person.Id) && personButtons[person.Id] != null)
                    {
                        var btn = personButtons[person.Id];
                        btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700"));
                        btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8C00"));
                        btn.BorderThickness = new Thickness(2);
                    }
                }

                txtSearch.ToolTip = $"Найдено: {matchingPersons.Count}";
                HideNoResultsNotification();

                if (matchingPersons.Count == 1)
                {
                    var personId = matchingPersons.First().Id;
                    selectedPersonId = personId;
                    ShowPersonDetails(personId);
                }
            }
            else
            {
                txtSearch.ToolTip = "Ничего не найдено";
                ShowNoResultsNotification(searchText);
            }
        }

        private void ShowNoResultsNotification(string searchText)
        {
            if (treeCanvas == null) return;

            if (searchNotificationTimer != null)
            {
                searchNotificationTimer.Stop();
                searchNotificationTimer.Tick -= Timer_Tick;
                searchNotificationTimer = null;
            }

            HideNoResultsNotification();

            Border notificationBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDF8F0")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7A48B")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Width = 300,
                Height = 100,
                Tag = "notification"
            };

            notificationBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 10,
                Opacity = 0.2,
                ShadowDepth = 3,
                Color = (Color)ColorConverter.ConvertFromString("#5C4E3D")
            };

            StackPanel stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock titleText = new TextBlock
            {
                Text = "😕 Ничего не найдено",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C4E3D")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            TextBlock messageText = new TextBlock
            {
                Text = $"По запросу \"{searchText}\" ничего не найдено",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B7E6B")),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(messageText);
            notificationBorder.Child = stackPanel;

            Canvas.SetLeft(notificationBorder, (treeCanvas.Width - notificationBorder.Width) / 2);
            Canvas.SetTop(notificationBorder, (treeCanvas.Height - notificationBorder.Height) / 2);
            Canvas.SetZIndex(notificationBorder, 100);

            treeCanvas.Children.Add(notificationBorder);

            searchNotificationTimer = new System.Windows.Threading.DispatcherTimer();
            searchNotificationTimer.Interval = TimeSpan.FromSeconds(3);
            searchNotificationTimer.Tick += Timer_Tick;
            searchNotificationTimer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (searchNotificationTimer != null)
            {
                searchNotificationTimer.Stop();
                searchNotificationTimer.Tick -= Timer_Tick;
                searchNotificationTimer = null;
            }
            HideNoResultsNotification();
        }

        private void HideNoResultsNotification()
        {
            if (treeCanvas == null) return;

            var notificationToRemove = treeCanvas.Children.OfType<Border>()
                .FirstOrDefault(b => b.Tag?.ToString() == "notification");

            if (notificationToRemove != null)
            {
                treeCanvas.Children.Remove(notificationToRemove);
            }
        }

        private void ClearSearchHighlight()
        {
            if (personButtons == null) return;

            foreach (var button in personButtons.Values)
            {
                if (button != null)
                {
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDF8F0"));
                    button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7A48B"));
                    button.BorderThickness = new Thickness(1);
                }
            }
            HideNoResultsNotification();
        }

        // ==================== ПЕРЕТАСКИВАНИЕ ====================

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
            Canvas.SetZIndex(draggedButton, 10);
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
            double offsetX = currentPos.X - dragStartPoint.X;
            double offsetY = currentPos.Y - dragStartPoint.Y;

            double newLeft = Canvas.GetLeft(draggedButton) + offsetX;
            double newTop = Canvas.GetTop(draggedButton) + offsetY;

            newLeft = Math.Max(0, Math.Min(treeCanvas.Width - draggedButton.Width, newLeft));
            newTop = Math.Max(0, Math.Min(treeCanvas.Height - draggedButton.Height, newTop));

            Canvas.SetLeft(draggedButton, newLeft);
            Canvas.SetTop(draggedButton, newTop);
            dragStartPoint = currentPos;
            e.Handled = true;
        }

        private void PersonButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDragging || draggedButton == null) return;

            if (draggedButton.Tag is int personId)
            {
                // Сохраняем новые позиции (можно реализовать сохранение в БД)
                MessageBox.Show($"Персона перемещена. X={Canvas.GetLeft(draggedButton):F0}, Y={Canvas.GetTop(draggedButton):F0}",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Canvas.SetZIndex(draggedButton, 1);
            isDragging = false;
            draggedButton.ReleaseMouseCapture();
            draggedButton = null;
            e.Handled = true;
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
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

        // ==================== ОБРАБОТЧИКИ КЛИКОВ ====================

        private void PersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (isDragging) return;

            var btn = sender as Button;
            if (btn?.Tag == null) return;

            int personId = (int)btn.Tag;
            selectedPersonId = personId;

            foreach (var button in personButtons.Values)
            {
                if (button != null)
                    button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDF8F0"));
            }

            if (btn != null)
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

            contextMenu.Items.Add(editItem);
            contextMenu.Items.Add(storyItem);

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

        // ==================== МЕТОД ПОКАЗА ДЕТАЛЕЙ ====================
        private void ShowPersonDetails(int personId)
        {
            try
            {
                // Очищаем старые данные
                txtPersonName.Text = "Загрузка...";
                txtBirthDate.Text = "--";
                txtDeathDate.Text = "--";
                txtGender.Text = "";
                txtGenderSymbol.Text = "👤";
                txtParents.Text = "Родители: --";
                txtSpouse.Text = "Супруг(а): --";
                txtChildren.Text = "Дети: --";
                imgProfile.Visibility = Visibility.Collapsed;
                txtNoPhoto.Visibility = Visibility.Visible;

                using (var context = new GenealogyDBEntities())
                {
                    var person = context.Persons.FirstOrDefault(p => p.Id == personId);
                    if (person == null)
                    {
                        txtPersonName.Text = "Персона не найдена";
                        return;
                    }

                    // Основная информация
                    txtPersonName.Text = $"{person.LastName} {person.FirstName} {person.Patronymic}".Trim();
                    txtBirthDate.Text = person.BirthDate?.ToString("dd.MM.yyyy") ?? "?";
                    txtDeathDate.Text = person.DeathDate?.ToString("dd.MM.yyyy") ?? "...";

                    var gender = context.Genders.FirstOrDefault(g => g.Id == person.GenderId);
                    if (gender != null)
                    {
                        txtGender.Text = gender.Name;
                        txtGenderSymbol.Text = gender.Symbol ?? "👤";
                    }

                    // Фото профиля
                    if (!string.IsNullOrEmpty(person.ProfilePhotoPath))
                    {
                        try
                        {
                            string fullPath = FileHelper.GetFullFilePath(person.ProfilePhotoPath);
                            if (System.IO.File.Exists(fullPath))
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.UriSource = new Uri(fullPath);
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.EndInit();

                                imgProfile.Source = bitmap;
                                imgProfile.Visibility = Visibility.Visible;
                                txtNoPhoto.Visibility = Visibility.Collapsed;
                            }
                        }
                        catch { }
                    }

                    // РОДИТЕЛИ
                    var parentRelations = context.Relationships
                        .Where(r => r.Person2Id == personId && r.RelationshipType == 1)
                        .Select(r => r.Person1Id)
                        .ToList();

                    if (parentRelations.Any())
                    {
                        var parents = context.Persons
                            .Where(p => parentRelations.Contains(p.Id))
                            .ToList();

                        if (parents.Any())
                        {
                            var parentNames = new List<string>();
                            foreach (var parent in parents)
                            {
                                parentNames.Add($"{parent.FirstName} {parent.LastName}");
                            }
                            txtParents.Text = $"Родители: {string.Join(", ", parentNames)}";
                        }
                        else
                        {
                            txtParents.Text = "Родители: нет данных";
                        }
                    }
                    else
                    {
                        txtParents.Text = "Родители: нет данных";
                    }

                    // СУПРУГ(А)
                    var spouseRel = context.Relationships
                        .FirstOrDefault(r => (r.Person1Id == personId || r.Person2Id == personId)
                                           && r.RelationshipType == 2);

                    if (spouseRel != null)
                    {
                        int spouseId = spouseRel.Person1Id == personId ? spouseRel.Person2Id : spouseRel.Person1Id;
                        var spouse = context.Persons.FirstOrDefault(p => p.Id == spouseId);
                        if (spouse != null)
                        {
                            txtSpouse.Text = $"Супруг(а): {spouse.FirstName} {spouse.LastName}";
                        }
                        else
                        {
                            txtSpouse.Text = "Супруг(а): нет";
                        }
                    }
                    else
                    {
                        txtSpouse.Text = "Супруг(а): нет";
                    }

                    // ДЕТИ
                    var childRelations = context.Relationships
                        .Where(r => r.Person1Id == personId && r.RelationshipType == 1)
                        .Select(r => r.Person2Id)
                        .ToList();

                    if (childRelations.Any())
                    {
                        var children = context.Persons
                            .Where(p => childRelations.Contains(p.Id))
                            .ToList();

                        if (children.Any())
                        {
                            var childNames = new List<string>();
                            foreach (var child in children)
                            {
                                childNames.Add($"{child.FirstName} {child.LastName}");
                            }
                            txtChildren.Text = $"Дети: {string.Join(", ", childNames)}";
                        }
                        else
                        {
                            txtChildren.Text = "Дети: нет";
                        }
                    }
                    else
                    {
                        txtChildren.Text = "Дети: нет";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void ShowEmptyMessage()
        {
            if (treeCanvas == null) return;

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

        // ==================== ОБРАБОТЧИКИ КНОПОК ====================

        private void MyTreesButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new TreesPage());
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ReportsPage());
        }

        private void AddPersonButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new EditPersonPage());
        }

        private void EditPersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPersonId.HasValue)
                NavigationService.Navigate(new EditPersonPage(selectedPersonId.Value));
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

        private void EditPerson(int id)
        {
            NavigationService.Navigate(new EditPersonPage(id));
        }

        private void DeletePerson(int id)
        {
            var result = MessageBox.Show("Удалить эту персону?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new GenealogyDBEntities())
                    {
                        var person = context.Persons.Find(id);
                        if (person != null)
                        {
                            context.Persons.Remove(person);
                            context.SaveChanges();
                            MessageBox.Show("Персона удалена");
                            LoadTree();

                            txtPersonName.Text = "Выберите персону";
                            txtBirthDate.Text = "--";
                            txtDeathDate.Text = "--";
                            txtGender.Text = "Не указан";
                            txtParents.Text = "Родители: --";
                            txtSpouse.Text = "Супруг(а): --";
                            txtChildren.Text = "Дети: --";
                            selectedPersonId = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}");
                }
            }
        }

        private void AddStory(int id)
        {
            NavigationService.Navigate(new EditStoryPage(id));
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