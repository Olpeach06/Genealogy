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
        private int currentTreeId = 0;
        private int? selectedPersonId = null;
        private Dictionary<int, Border> personCards = new Dictionary<int, Border>(); // Border вместо Button
        private double zoomLevel = 1.0;

        // Для перетаскивания
        private bool isDragging = false;
        private Border draggedCard = null; // Border вместо Button
        private Point dragStartPoint;
        private bool isCanvasDragging = false;
        private Point canvasDragStart;
        private ScrollViewer parentScrollViewer;

        // Для поиска
        private string currentSearchText = "";
        private List<Persons> allPersons = new List<Persons>();
        private System.Windows.Threading.DispatcherTimer searchTimer;

        // Для фильтрации
        private Dictionary<int, int> personGenerations = new Dictionary<int, int>();
        private List<Relationships> allRelationships = new List<Relationships>();
        private int? currentGenerationFilter = null;

        // Для группировки семей
        private Dictionary<int, int> spousePairs = new Dictionary<int, int>();
        private Dictionary<int, FamilyNode> familyTree = new Dictionary<int, FamilyNode>();

        // Флаг, указывающий, что страница полностью загружена
        private bool isPageLoaded = false;

        // Класс для представления узла семьи
        public class FamilyNode
        {
            public int PersonId { get; set; }
            public Persons Person { get; set; }
            public int? SpouseId { get; set; }
            public List<int> Children { get; set; } = new List<int>();
            public List<int> Parents { get; set; } = new List<int>();
            public int Generation { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }

        public MainPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;

            searchTimer = new System.Windows.Threading.DispatcherTimer();
            searchTimer.Interval = TimeSpan.FromMilliseconds(500);
            searchTimer.Tick += SearchTimer_Tick;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (Session.IsGuest)
            {
                txtUserName.Text = "Гость";
                btnAdd.Visibility = Visibility.Collapsed;
                btnEdit.Visibility = Visibility.Collapsed;
                btnDelete.Visibility = Visibility.Collapsed;
                btnSideEdit.Visibility = Visibility.Collapsed;
                btnSideStory.Visibility = Visibility.Collapsed;
                btnUsers.Visibility = Visibility.Collapsed;
                ShowFirstAvailableTree();
                return;
            }
            else
            {
                txtUserName.Text = Session.Username;
            }

            bool canEdit = Session.IsAdmin || Session.IsEditor;
            bool isAdmin = Session.IsAdmin;

            btnAdd.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnEdit.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnDelete.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnSideEdit.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnSideStory.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
            btnUsers.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;

            currentTreeId = Session.CurrentTreeId;

            if (currentTreeId == 0)
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
                        ShowNoTreesMessage();
                        return;
                    }
                }
            }

            parentScrollViewer = FindVisualChild<ScrollViewer>(this);
            LoadTree();
            isPageLoaded = true;
        }

        private void ShowFirstAvailableTree()
        {
            try
            {
                using (var context = new GenealogyDBEntities())
                {
                    var firstTree = context.FamilyTrees
                        .OrderBy(t => t.Id)
                        .FirstOrDefault();

                    if (firstTree != null)
                    {
                        currentTreeId = firstTree.Id;
                        parentScrollViewer = FindVisualChild<ScrollViewer>(this);
                        LoadTree();
                        isPageLoaded = true;
                    }
                    else
                    {
                        treeCanvas.Children.Clear();
                        var tb = new TextBlock
                        {
                            Text = "В системе пока нет деревьев",
                            FontSize = 18,
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B7E6B")),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Canvas.SetLeft(tb, 400);
                        Canvas.SetTop(tb, 300);
                        treeCanvas.Children.Add(tb);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки дерева: {ex.Message}");
            }
        }

        private void ShowNoTreesMessage()
        {
            treeCanvas.Children.Clear();
            var tb = new TextBlock
            {
                Text = "У вас пока нет деревьев. Перейдите в раздел 'Мои деревья' чтобы создать новое",
                FontSize = 18,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B7E6B")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(tb, 300);
            Canvas.SetTop(tb, 300);
            treeCanvas.Children.Add(tb);
        }

        private void LoadTree()
        {
            if (treeCanvas == null) return;

            treeCanvas.Children.Clear();
            personCards.Clear();
            allPersons.Clear();
            allRelationships.Clear();
            spousePairs.Clear();
            familyTree.Clear();
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

                    FindSpousePairs();
                    BuildFamilyTree();
                    DetermineGenerations();
                    CalculatePositions();
                    DrawAllRelationships();
                    DrawPersonCards();

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

        private void BuildFamilyTree()
        {
            foreach (var person in allPersons)
            {
                familyTree[person.Id] = new FamilyNode
                {
                    PersonId = person.Id,
                    Person = person,
                    SpouseId = spousePairs.ContainsKey(person.Id) ? spousePairs[person.Id] : (int?)null,
                    Children = new List<int>(),
                    Parents = new List<int>(),
                    Generation = -1,
                    X = 0,
                    Y = 0
                };
            }

            foreach (var rel in allRelationships.Where(r => r.RelationshipType == 1))
            {
                if (familyTree.ContainsKey(rel.Person1Id) && familyTree.ContainsKey(rel.Person2Id))
                {
                    if (!familyTree[rel.Person1Id].Children.Contains(rel.Person2Id))
                        familyTree[rel.Person1Id].Children.Add(rel.Person2Id);

                    if (!familyTree[rel.Person2Id].Parents.Contains(rel.Person1Id))
                        familyTree[rel.Person2Id].Parents.Add(rel.Person1Id);
                }
            }
        }

        private void DetermineGenerations()
        {
            var roots = familyTree.Values.Where(n => n.Parents.Count == 0).ToList();

            if (!roots.Any())
            {
                var oldest = familyTree.Values
                    .Where(n => n.Person.BirthDate.HasValue)
                    .OrderBy(n => n.Person.BirthDate)
                    .FirstOrDefault();

                if (oldest != null)
                    roots.Add(oldest);
                else if (familyTree.Any())
                    roots.Add(familyTree.Values.First());
            }

            if (!roots.Any()) return;

            var queue = new Queue<FamilyNode>();
            foreach (var root in roots)
            {
                root.Generation = 0;
                queue.Enqueue(root);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var childId in current.Children)
                {
                    if (familyTree.TryGetValue(childId, out var child))
                    {
                        int newGen = current.Generation + 1;
                        if (child.Generation < newGen)
                        {
                            child.Generation = newGen;
                            queue.Enqueue(child);
                        }
                    }
                }
            }

            int maxGen = familyTree.Values.Max(n => n.Generation);
            foreach (var node in familyTree.Values)
            {
                if (node.Generation == -1)
                {
                    if (node.Person.BirthDate.HasValue)
                    {
                        int year = node.Person.BirthDate.Value.Year;
                        if (year < 1950) node.Generation = 0;
                        else if (year < 1980) node.Generation = 1;
                        else if (year < 2000) node.Generation = 2;
                        else if (year < 2020) node.Generation = 3;
                        else node.Generation = 4;
                    }
                    else
                    {
                        node.Generation = maxGen + 1;
                    }
                }
            }

            personGenerations = familyTree.ToDictionary(kv => kv.Key, kv => kv.Value.Generation);
        }

        private void CalculatePositions()
        {
            var generations = familyTree.Values
                .GroupBy(n => n.Generation)
                .OrderBy(g => g.Key)
                .ToList();

            int startX = 300;
            int startY = 100;
            int verticalSpacing = 180;

            foreach (var genGroup in generations)
            {
                int genLevel = genGroup.Key;
                var nodesInGen = genGroup.ToList();
                nodesInGen = nodesInGen.OrderBy(n => n.Person.BirthDate).ToList();

                var familiesInGen = new List<List<FamilyNode>>();
                var used = new HashSet<int>();

                foreach (var node in nodesInGen)
                {
                    if (used.Contains(node.PersonId)) continue;

                    var family = new List<FamilyNode> { node };

                    if (node.SpouseId.HasValue)
                    {
                        var spouse = nodesInGen.FirstOrDefault(n => n.PersonId == node.SpouseId.Value);
                        if (spouse != null && !used.Contains(spouse.PersonId))
                        {
                            family.Add(spouse);
                            used.Add(spouse.PersonId);
                        }
                    }

                    familiesInGen.Add(family);
                    used.Add(node.PersonId);
                }

                int currentX = startX;

                foreach (var family in familiesInGen)
                {
                    if (family.Count == 2)
                    {
                        var person1 = family[0];
                        var person2 = family[1];
                        var sorted = family.OrderBy(f => f.Person.BirthDate).ToList();
                        person1 = sorted[0];
                        person2 = sorted[1];

                        person1.X = currentX;
                        person2.X = currentX + 140;
                        double yPos = startY + genLevel * verticalSpacing;
                        person1.Y = yPos;
                        person2.Y = yPos;
                        currentX += 280;
                    }
                    else if (family.Count == 1)
                    {
                        var person = family[0];
                        person.X = currentX + 70;
                        person.Y = startY + genLevel * verticalSpacing;
                        currentX += 200;
                    }
                }
            }

            foreach (var node in familyTree.Values.OrderByDescending(n => n.Generation))
            {
                if (node.Children.Any())
                {
                    var children = node.Children
                        .Where(id => familyTree.ContainsKey(id))
                        .Select(id => familyTree[id])
                        .Where(c => c.X > 0)
                        .ToList();

                    if (children.Any())
                    {
                        double minChildX = children.Min(c => c.X);
                        double maxChildX = children.Max(c => c.X);
                        double centerX = (minChildX + maxChildX) / 2;

                        if (node.SpouseId.HasValue && familyTree.TryGetValue(node.SpouseId.Value, out var spouse))
                        {
                            double parentCenterX = (node.X + spouse.X) / 2;
                            double offset = centerX - parentCenterX;
                            node.X += offset;
                            spouse.X += offset;
                        }
                        else
                        {
                            double offset = centerX - node.X;
                            node.X += offset;
                        }
                    }
                }
            }

            foreach (var genGroup in generations)
            {
                var nodesInGen = genGroup.ToList();
                double minSpacing = 140;

                for (int i = 0; i < nodesInGen.Count - 1; i++)
                {
                    for (int j = i + 1; j < nodesInGen.Count; j++)
                    {
                        double distance = Math.Abs(nodesInGen[i].X - nodesInGen[j].X);
                        if (distance < minSpacing && distance > 0)
                        {
                            double shift = (minSpacing - distance) / 2;
                            nodesInGen[i].X -= shift;
                            nodesInGen[j].X += shift;
                        }
                    }
                }
            }

            double overallMinX = familyTree.Values.Min(n => n.X);
            if (overallMinX < 100)
            {
                double shiftAmount = 100 - overallMinX;
                foreach (var node in familyTree.Values)
                {
                    node.X += shiftAmount;
                }
            }
        }

        private void DrawAllRelationships()
        {
            if (treeCanvas == null) return;

            foreach (var rel in allRelationships)
            {
                if (!familyTree.ContainsKey(rel.Person1Id) || !familyTree.ContainsKey(rel.Person2Id))
                    continue;

                var person1 = familyTree[rel.Person1Id];
                var person2 = familyTree[rel.Person2Id];

                // Используем актуальные координаты из familyTree
                Point p1 = new Point(person1.X + 60, person1.Y + 30);
                Point p2 = new Point(person2.X + 60, person2.Y + 30);

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

                if (rel.RelationshipType == 1)
                {
                    line.StrokeDashArray = new DoubleCollection { 2, 2 };
                }

                Canvas.SetZIndex(line, 0);
                treeCanvas.Children.Add(line);
            }
        }

        private Brush GetLineColor(int type)
        {
            if (type == 1)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C4E3D"));
            else if (type == 2)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8FAA7A"));
            else
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B7E6B"));
        }

        // ==================== ОТРИСОВКА КАРТОЧЕК ПЕРСОН ====================

        private void DrawPersonCards()
        {
            if (treeCanvas == null) return;

            foreach (var person in allPersons)
            {
                if (!familyTree.ContainsKey(person.Id)) continue;

                var node = familyTree[person.Id];
                Point pos = new Point(node.X, node.Y);

                string genderSymbol = "👤";
                if (person.GenderId == 1) genderSymbol = "♂";
                else if (person.GenderId == 2) genderSymbol = "♀";

                string birthYear = person.BirthDate?.Year.ToString() ?? "?";
                string deathYear = person.DeathDate?.Year.ToString() ?? "";

                string dateInfo = birthYear;
                if (!string.IsNullOrEmpty(deathYear))
                    dateInfo += $" - {deathYear}";

                // Создаем Border как контейнер для карточки персоны
                var cardBorder = new Border
                {
                    Width = 120,
                    Height = 80,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDF8F0")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7A48B")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Tag = person.Id,
                    Cursor = Cursors.Hand
                };

                // Создаем Grid для внутреннего содержимого
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Текст с информацией
                var textBlock = new TextBlock
                {
                    Text = $"{person.FirstName} {person.LastName}\n{genderSymbol} {dateInfo}",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C4E3D")),
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(5)
                };
                Grid.SetRow(textBlock, 0);
                grid.Children.Add(textBlock);

                // Кнопка выбора (под текстом)
                var selectButton = new Button
                {
                    Content = "Выбрать",
                    Width = 80,
                    Height = 22,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8FAA7A")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 10,
                    Tag = person.Id,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                selectButton.Click += PersonSelect_Click;
                Grid.SetRow(selectButton, 1);
                grid.Children.Add(selectButton);

                cardBorder.Child = grid;

                // Обработчики для перетаскивания на самом Border
                cardBorder.MouseLeftButtonDown += PersonBorder_MouseLeftButtonDown;
                cardBorder.MouseLeftButtonUp += PersonBorder_MouseLeftButtonUp;
                cardBorder.MouseMove += PersonBorder_MouseMove;

                Canvas.SetLeft(cardBorder, pos.X);
                Canvas.SetTop(cardBorder, pos.Y);
                Canvas.SetZIndex(cardBorder, 1);

                treeCanvas.Children.Add(cardBorder);
                personCards[person.Id] = cardBorder;
            }
        }

        // ==================== ПЕРЕТАСКИВАНИЕ КАРТОЧЕК ====================

        private void PersonBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Session.IsGuest || !(Session.IsAdmin || Session.IsEditor))
            {
                e.Handled = true;
                return;
            }

            var border = sender as Border;
            if (border == null) return;

            isDragging = true;
            draggedCard = border;
            dragStartPoint = e.GetPosition(treeCanvas);
            Canvas.SetZIndex(draggedCard, 10);
            border.CaptureMouse();
            e.Handled = true;
        }

        private void PersonBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || draggedCard == null) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                isDragging = false;
                draggedCard.ReleaseMouseCapture();
                Canvas.SetZIndex(draggedCard, 1);
                draggedCard = null;
                return;
            }

            Point currentPos = e.GetPosition(treeCanvas);
            double offsetX = currentPos.X - dragStartPoint.X;
            double offsetY = currentPos.Y - dragStartPoint.Y;

            double newLeft = Canvas.GetLeft(draggedCard) + offsetX;
            double newTop = Canvas.GetTop(draggedCard) + offsetY;

            newLeft = Math.Max(0, Math.Min(treeCanvas.Width - draggedCard.Width, newLeft));
            newTop = Math.Max(0, Math.Min(treeCanvas.Height - draggedCard.Height, newTop));

            Canvas.SetLeft(draggedCard, newLeft);
            Canvas.SetTop(draggedCard, newTop);
            dragStartPoint = currentPos;
            e.Handled = true;
        }

        private void PersonBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDragging || draggedCard == null) return;

            if (draggedCard.Tag is int personId && familyTree.ContainsKey(personId))
            {
                // Сохраняем новые координаты
                familyTree[personId].X = Canvas.GetLeft(draggedCard);
                familyTree[personId].Y = Canvas.GetTop(draggedCard);

                // ПЕРЕРИСОВЫВАЕМ ЛИНИИ
                RedrawAllLines();
            }

            Canvas.SetZIndex(draggedCard, 1);
            isDragging = false;
            draggedCard.ReleaseMouseCapture();
            draggedCard = null;
            e.Handled = true;
        }

        // ==================== ПЕРЕРИСОВКА ЛИНИЙ ====================

        private void RedrawAllLines()
        {
            if (treeCanvas == null) return;

            // Удаляем все существующие линии
            var linesToRemove = treeCanvas.Children.OfType<Line>().ToList();
            foreach (var line in linesToRemove)
            {
                treeCanvas.Children.Remove(line);
            }

            // Перерисовываем все связи заново
            DrawAllRelationships();
        }

        // ==================== ВЫБОР ПЕРСОНЫ ====================

        private void PersonSelect_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            int personId = (int)button.Tag;
            selectedPersonId = personId;

            // Сброс фона всех карточек
            foreach (var card in personCards.Values)
            {
                if (card != null)
                    card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDF8F0"));
            }

            // Подсветка выбранной карточки
            if (personCards.ContainsKey(personId) && personCards[personId] != null)
            {
                personCards[personId].Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AFC09A"));
            }

            ShowPersonDetails(personId);
        }

        // ==================== ОСТАЛЬНЫЕ МЕТОДЫ (БЕЗ ИЗМЕНЕНИЙ) ====================

        private void ShowPersonDetails(int personId)
        {
            try
            {
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

                    txtPersonName.Text = $"{person.LastName} {person.FirstName} {person.Patronymic}".Trim();
                    txtBirthDate.Text = person.BirthDate?.ToString("dd.MM.yyyy") ?? "?";
                    txtDeathDate.Text = person.DeathDate?.ToString("dd.MM.yyyy") ?? "...";

                    var gender = context.Genders.FirstOrDefault(g => g.Id == person.GenderId);
                    if (gender != null)
                    {
                        txtGender.Text = gender.Name;
                        txtGenderSymbol.Text = gender.Symbol ?? "👤";
                    }

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

                    var parentRelations = context.Relationships
                        .Where(r => r.Person2Id == personId && r.RelationshipType == 1)
                        .Select(r => r.Person1Id)
                        .ToList();

                    if (parentRelations.Any())
                    {
                        var parents = context.Persons.Where(p => parentRelations.Contains(p.Id)).ToList();
                        if (parents.Any())
                        {
                            var parentNames = new List<string>();
                            foreach (var parent in parents) parentNames.Add($"{parent.FirstName} {parent.LastName}");
                            txtParents.Text = $"Родители: {string.Join(", ", parentNames)}";
                        }
                        else txtParents.Text = "Родители: нет данных";
                    }
                    else txtParents.Text = "Родители: нет данных";

                    var spouseRel = context.Relationships
                        .FirstOrDefault(r => (r.Person1Id == personId || r.Person2Id == personId) && r.RelationshipType == 2);

                    if (spouseRel != null)
                    {
                        int spouseId = spouseRel.Person1Id == personId ? spouseRel.Person2Id : spouseRel.Person1Id;
                        var spouse = context.Persons.FirstOrDefault(p => p.Id == spouseId);
                        if (spouse != null) txtSpouse.Text = $"Супруг(а): {spouse.FirstName} {spouse.LastName}";
                        else txtSpouse.Text = "Супруг(а): нет";
                    }
                    else txtSpouse.Text = "Супруг(а): нет";

                    var childRelations = context.Relationships
                        .Where(r => r.Person1Id == personId && r.RelationshipType == 1)
                        .Select(r => r.Person2Id)
                        .ToList();

                    if (childRelations.Any())
                    {
                        var children = context.Persons.Where(p => childRelations.Contains(p.Id)).ToList();
                        if (children.Any())
                        {
                            var childNames = new List<string>();
                            foreach (var child in children) childNames.Add($"{child.FirstName} {child.LastName}");
                            txtChildren.Text = $"Дети: {string.Join(", ", childNames)}";
                        }
                        else txtChildren.Text = "Дети: нет";
                    }
                    else txtChildren.Text = "Дети: нет";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        // ==================== ФИЛЬТРАЦИЯ ====================

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!isPageLoaded || treeCanvas == null || personCards == null || !personCards.Any()) return;

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

            foreach (var card in personCards.Values)
                if (card != null) card.Visibility = Visibility.Visible;

            foreach (var child in treeCanvas.Children)
                if (child is Line line) line.Visibility = Visibility.Visible;
        }

        private void ApplyGenerationFilter(int generationLevel)
        {
            if (treeCanvas == null || familyTree == null || !familyTree.Any() || personCards == null) return;

            var personIdsInGeneration = familyTree.Values
                .Where(n => n.Generation == generationLevel)
                .Select(n => n.PersonId)
                .ToHashSet();

            foreach (var card in personCards.Values)
                if (card != null) card.Visibility = Visibility.Collapsed;

            foreach (var personId in personIdsInGeneration)
                if (personCards.ContainsKey(personId) && personCards[personId] != null)
                    personCards[personId].Visibility = Visibility.Visible;

            foreach (var child in treeCanvas.Children)
            {
                if (child is Line line && line.Tag != null)
                {
                    string tag = line.Tag.ToString();
                    var parts = tag.Split('_');
                    if (parts.Length == 3 && parts[0] == "line")
                    {
                        int id1 = int.Parse(parts[1]);
                        int id2 = int.Parse(parts[2]);
                        line.Visibility = (personIdsInGeneration.Contains(id1) && personIdsInGeneration.Contains(id2))
                            ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        // ==================== ПОИСК ====================

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            searchTimer.Stop();
            PerformSearch();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (btnClearSearch == null) return;

            if (txtSearch.Text != "Поиск..." && !string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                btnClearSearch.Visibility = Visibility.Visible;
                searchTimer.Stop();
                searchTimer.Start();
            }
            else
            {
                btnClearSearch.Visibility = Visibility.Collapsed;
                searchTimer.Stop();
                ClearSearchHighlight();
                currentSearchText = "";
                txtSearch.ToolTip = null;
                HideNoResultsNotification();
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "Поиск...";
            searchTimer.Stop();
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

            foreach (var card in personCards.Values)
            {
                if (card != null)
                {
                    card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDF8F0"));
                    card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7A48B"));
                    card.BorderThickness = new Thickness(1);
                }
            }

            if (matchingPersons.Any())
            {
                foreach (var person in matchingPersons)
                {
                    if (personCards.ContainsKey(person.Id) && personCards[person.Id] != null)
                    {
                        var card = personCards[person.Id];
                        card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700"));
                        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8C00"));
                        card.BorderThickness = new Thickness(2);
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

            var hideTimer = new System.Windows.Threading.DispatcherTimer();
            hideTimer.Interval = TimeSpan.FromSeconds(3);
            hideTimer.Tick += (s, args) =>
            {
                hideTimer.Stop();
                HideNoResultsNotification();
            };
            hideTimer.Start();
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
            if (personCards == null) return;

            foreach (var card in personCards.Values)
            {
                if (card != null)
                {
                    card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDF8F0"));
                    card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7A48B"));
                    card.BorderThickness = new Thickness(1);
                }
            }
            HideNoResultsNotification();
        }

        // ==================== ПЕРЕТАСКИВАНИЕ КАНВАСА ====================

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

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new UsersPage());
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
                DeletePersonWithRelationships(selectedPersonId.Value);
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

        private void DeletePersonWithRelationships(int id)
        {
            var result = MessageBox.Show("Удалить эту персону?\nВсе связанные с ней записи (родственные связи, истории, медиафайлы) будут также удалены!",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new GenealogyDBEntities())
                    {
                        var person = context.Persons.Find(id);
                        if (person == null)
                        {
                            MessageBox.Show("Персона не найдена!");
                            return;
                        }

                        var relationships = context.Relationships.Where(r => r.Person1Id == id || r.Person2Id == id).ToList();
                        context.Relationships.RemoveRange(relationships);

                        var stories = context.Stories.Where(s => s.PersonId == id).ToList();
                        context.Stories.RemoveRange(stories);

                        var mediaLinks = context.MediaLinks.Where(ml => ml.PersonId == id).ToList();
                        context.MediaLinks.RemoveRange(mediaLinks);

                        context.Persons.Remove(person);
                        context.SaveChanges();

                        MessageBox.Show("Персона и все связанные данные удалены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

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
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddStory(int id)
        {
            NavigationService.Navigate(new EditStoryPage(id));
        }

        private void txtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSearch.Text == "Поиск...") txtSearch.Text = "";
        }

        private void txtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text)) txtSearch.Text = "Поиск...";
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            Session.Clear();
            NavigationService.Navigate(new LoginPage());
        }
    }
}