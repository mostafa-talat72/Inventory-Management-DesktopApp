using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ProductApp.Controls;

/// <summary>
/// WrapPanel مع تفعيل الـ Virtualization — يرسم فقط العناصر الظاهرة
/// ويحافظ على ترتيب الصفوف/الأعمدة (يلزمه ScrollViewer داخل قالب
/// العنصر المتحكم أو ListView/ListBox ليعمل الـ IScrollInfo).
/// </summary>
public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    private const double ScrollLineDelta = 16.0;
    private const double MouseWheelDelta = 48.0;

    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(double.NaN,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(double.NaN,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    private sealed class ItemSlot
    {
        public int Index;
        public UIElement? Element;
    }

    private readonly List<ItemSlot> _realized = new();
    private bool _needRepeatMeasure = true;
    private bool _itemSizeKnown;
    private bool _itemsEventHooked;
    private double _childWidth = 200;
    private double _childHeight = 200;
    private Size _extent = new(0, 0);
    private Size _viewport = new(0, 0);
    private Point _offset;

    private IItemContainerGenerator Gen => ItemContainerGenerator;

    private ItemsControl? ItemsOwner => ItemsControl.GetItemsOwner(this);

    private int ItemCount => ItemsOwner?.Items.Count ?? 0;

    /// <summary>
    /// وصول داخلي لمجموعة Children. بمجرد ربط البانل بالمولد (IsDataBound)،
    /// كل الطرق العامة (Add/Remove/Clear) ترمي InvalidOperationException عبر
    /// VerifyWriteAccess؛ WPF ينص على استخدام الطرق الداخلية (AddInternal/
    /// RemoveInternal/ClearInternal) للبانل الذي يملأ المجموعة بالتعاون مع المولد.
    /// </summary>
    private static class ChildrenAccess
    {
        private static readonly MethodInfo? AddInternal =
            typeof(UIElementCollection).GetMethod("AddInternal", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo? RemoveInternal =
            typeof(UIElementCollection).GetMethod("RemoveInternal", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo? ClearInternal =
            typeof(UIElementCollection).GetMethod("ClearInternal", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Add(UIElementCollection collection, UIElement element)
            => AddInternal?.Invoke(collection, new object[] { element });

        public static void Remove(UIElementCollection collection, UIElement element)
            => RemoveInternal?.Invoke(collection, new object[] { element });

        public static void Clear(UIElementCollection collection)
            => ClearInternal?.Invoke(collection, null);
    }

    /// <summary>
    /// يضمن ربط البانل بالمولد. WPF يربط البانل كسولًا عبر
    /// Panel.InternalChildren → VerifyBoundState → EnsureGenerator → ConnectToGenerator
    /// (الذي يضبط _itemContainerGenerator ويشترك في ItemsChanged)، وأي وصول إلى
    /// Children/InternalChildren يشغّل هذا الربط. نلمس Children مرة واحدة هنا
    /// لأن قراءة ItemContainerGenerator مباشرةً لا تشغّل الربط أبدًا
    /// (الربط التلقائي عبر OnVisualParentChanged يفشل لأن IsItemsHost
    /// يُضبط بعد الإرفاق البصري في قوالب FrameworkElementFactory).
    /// </summary>
    private void EnsureGeneratorConnected()
    {
        if (ItemContainerGenerator != null) return;
        if (!IsItemsHost) return;
        if (ItemsOwner?.ItemContainerGenerator == null) return;

        try
        {
            _ = Children.Count;
        }
        catch (InvalidOperationException)
        {
            return; // ليس ItemsHost بعد — نعيد المحاولة في القياس التالي
        }
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(sender, args);
        if (args.Action == NotifyCollectionChangedAction.Reset)
            _needRepeatMeasure = true;
    }

    private void EnsureItemsEventHooked()
    {
        if (_itemsEventHooked) return;
        if (ItemsOwner?.Items is INotifyCollectionChanged icc)
        {
            icc.CollectionChanged += (_, _) =>
            {
                _needRepeatMeasure = true;
                InvalidateMeasure();
            };
            _itemsEventHooked = true;
        }
    }

    protected override void OnClearChildren()
    {
        base.OnClearChildren();
        _realized.Clear();
    }

    private void CleanupAll()
    {
        if (Children.Count == 0 && _realized.Count == 0) return;

        // نزيل من المولد بـ Remove وليس RemoveAll: RemoveAll يرفع Reset فيستدعي
        // ResetChildren → GenerateChildren فيعيد توليد الدفعة الكاملة من جديد.
        var gen = Gen;
        if (gen != null)
        {
            try
            {
                gen.Remove(new GeneratorPosition(0, 0), Children.Count);
            }
            catch
            {
                gen.RemoveAll();
            }
        }
        ChildrenAccess.Clear(Children);
        _realized.Clear();
    }

    private void EnsureItemSize()
    {
        if (_itemSizeKnown) return;
        if (ItemCount == 0) return;
        if (Gen == null) return;

        UIElement? child;
        using (Gen.StartAt(new GeneratorPosition(-1, 0), GeneratorDirection.Forward, true))
        {
            child = (UIElement?)Gen.GenerateNext(out bool isNew);
            if (child == null) return;
            if (isNew)
            {
                ChildrenAccess.Add(Children, child);
                Gen.PrepareItemContainer(child);
            }
            else if (!Children.Contains(child))
            {
                ChildrenAccess.Add(Children, child);
            }
        }

        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _childWidth = double.IsNaN(ItemWidth) ? child.DesiredSize.Width : ItemWidth;
        _childHeight = double.IsNaN(ItemHeight) ? child.DesiredSize.Height : ItemHeight;
        if (_childWidth <= 0) _childWidth = 200;
        if (_childHeight <= 0) _childHeight = 200;

        _itemSizeKnown = true;
        _realized.Add(new ItemSlot { Index = 0, Element = child });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureGeneratorConnected();
        if (ItemContainerGenerator == null)
            return base.MeasureOverride(availableSize);

        EnsureItemsEventHooked();

        int itemCount = ItemCount;
        if (itemCount == 0)
        {
            CleanupAll();
            UpdateScrollInfo(new Size(0, 0), availableSize);
            return availableSize;
        }

        if (_needRepeatMeasure)
        {
            CleanupAll();
            _itemSizeKnown = false;
            _needRepeatMeasure = false;
        }

        EnsureItemSize();

        double itemWidth = _childWidth;
        double itemHeight = _childHeight;
        int itemsPerRow = Math.Max(1, (int)Math.Floor(Math.Max(1.0, availableSize.Width) / itemWidth));
        double extentWidth = Math.Max(availableSize.Width, itemsPerRow * itemWidth);
        double extentHeight = Math.Ceiling((double)itemCount / itemsPerRow) * itemHeight;
        var extent = new Size(extentWidth, extentHeight);

        double viewportHeight = Math.Max(1.0, availableSize.Height);
        int firstVisibleIndex = Math.Max(0, (int)(_offset.Y / itemHeight) * itemsPerRow);
        int lastVisibleIndex = Math.Min(itemCount - 1,
            (int)Math.Ceiling((_offset.Y + viewportHeight) / itemHeight) * itemsPerRow + itemsPerRow - 1);
        int firstIndex = Math.Max(0, firstVisibleIndex - itemsPerRow);
        int lastIndex = Math.Min(itemCount - 1, lastVisibleIndex + itemsPerRow);

        RealizeRange(firstIndex, lastIndex);

        foreach (var slot in _realized)
            slot.Element?.Measure(new Size(itemWidth, itemHeight));

        UpdateScrollInfo(extent, availableSize);
        return availableSize;
    }

    private void RealizeRange(int firstIndex, int lastIndex)
    {
        if (_realized.Count > 0)
        {
            int rFirst = _realized[0].Index;
            int rLast = _realized[^1].Index;

            if (rFirst < firstIndex)
            {
                int cut = Math.Min(firstIndex, rLast + 1) - rFirst;
                for (int i = 0; i < cut; i++)
                    if (_realized[i].Element != null) ChildrenAccess.Remove(Children, _realized[i].Element!);
                Gen?.Remove(new GeneratorPosition(0, 0), cut);
                _realized.RemoveRange(0, cut);
            }

            if (_realized.Count > 0 && rLast > lastIndex)
            {
                int cut = rLast - lastIndex;
                int start = _realized.Count - cut;
                for (int i = start; i < _realized.Count; i++)
                    if (_realized[i].Element != null) ChildrenAccess.Remove(Children, _realized[i].Element!);
                Gen?.Remove(new GeneratorPosition(start, 0), cut);
                _realized.RemoveRange(start, cut);
            }
        }

        if (Gen == null) return;

        using (Gen.StartAt(new GeneratorPosition(-1, firstIndex), GeneratorDirection.Forward, true))
        {
            for (int index = firstIndex; index <= lastIndex; index++)
            {
                var child = (UIElement?)Gen.GenerateNext(out bool isNew);
                if (child == null) break;
                if (isNew)
                {
                    ChildrenAccess.Add(Children, child);
                    Gen.PrepareItemContainer(child);
                }
                else if (!Children.Contains(child))
                {
                    ChildrenAccess.Add(Children, child);
                }

                var existing = _realized.Find(s => s.Index == index);
                if (existing != null)
                    existing.Element = child;
                else
                    _realized.Add(new ItemSlot { Index = index, Element = child });
            }
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_childWidth <= 0 || _childHeight <= 0) return finalSize;
        int itemsPerRow = Math.Max(1, (int)Math.Floor(Math.Max(1.0, finalSize.Width) / _childWidth));
        int itemCount = ItemCount;
        int lastRow = Math.Max(0, (itemCount - 1) / itemsPerRow);
        int lastRowCount = itemCount - lastRow * itemsPerRow;

        foreach (var slot in _realized)
        {
            if (slot.Element == null) continue;
            int row = slot.Index / itemsPerRow;
            int col = slot.Index % itemsPerRow;
            int rowCount = row == lastRow ? lastRowCount : itemsPerRow;
            double rowWidth = rowCount * _childWidth;
            double startX = Math.Max(0, (finalSize.Width - rowWidth) / 2);
            double x = startX + col * _childWidth - _offset.X;
            double y = row * _childHeight - _offset.Y;
            slot.Element.Arrange(new Rect(new Point(x, y), new Size(_childWidth, _childHeight)));
        }
        return finalSize;
    }

    private void UpdateScrollInfo(Size extent, Size viewport)
    {
        _extent = extent;
        _viewport = viewport;

        if (!double.IsInfinity(_extent.Width) && !double.IsInfinity(_viewport.Width))
            _offset.X = Math.Min(_offset.X, Math.Max(0, _extent.Width - _viewport.Width));
        if (!double.IsInfinity(_extent.Height) && !double.IsInfinity(_viewport.Height))
            _offset.Y = Math.Min(_offset.Y, Math.Max(0, _extent.Height - _viewport.Height));

        ScrollOwner?.InvalidateScrollInfo();
    }

    // ═══════════ IScrollInfo ═══════════

    public bool CanHorizontallyScroll { get; set; } = false;
    public bool CanVerticallyScroll { get; set; } = true;
    public double ExtentWidth => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => _offset.X;
    public double VerticalOffset => _offset.Y;
    public ScrollViewer? ScrollOwner { get; set; }

    public void LineUp() => SetVerticalOffset(_offset.Y - ScrollLineDelta);
    public void LineDown() => SetVerticalOffset(_offset.Y + ScrollLineDelta);
    public void LineLeft() => SetHorizontalOffset(_offset.X - ScrollLineDelta);
    public void LineRight() => SetHorizontalOffset(_offset.X + ScrollLineDelta);
    public void MouseWheelUp() => SetVerticalOffset(_offset.Y - MouseWheelDelta);
    public void MouseWheelDown() => SetVerticalOffset(_offset.Y + MouseWheelDelta);
    public void MouseWheelLeft() => SetHorizontalOffset(_offset.X - MouseWheelDelta);
    public void MouseWheelRight() => SetHorizontalOffset(_offset.X + MouseWheelDelta);
    public void PageUp() => SetVerticalOffset(_offset.Y - _viewport.Height);
    public void PageDown() => SetVerticalOffset(_offset.Y + _viewport.Height);
    public void PageLeft() => SetHorizontalOffset(_offset.X - _viewport.Width);
    public void PageRight() => SetHorizontalOffset(_offset.X + _viewport.Width);

    public void SetHorizontalOffset(double offset)
    {
        if (offset < 0 || _viewport.Width >= _extent.Width) offset = 0;
        if (offset > _extent.Width - _viewport.Width) offset = _extent.Width - _viewport.Width;
        _offset.X = offset;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public void SetVerticalOffset(double offset)
    {
        if (offset < 0 || _viewport.Height >= _extent.Height) offset = 0;
        if (offset > _extent.Height - _viewport.Height) offset = _extent.Height - _viewport.Height;
        _offset.Y = offset;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        for (int i = 0; i < _realized.Count; i++)
        {
            var slot = _realized[i];
            if (slot.Element == null || !slot.Element.IsAncestorOf(visual)) continue;

            int itemsPerRow = Math.Max(1, (int)Math.Floor(Math.Max(1.0, ViewportWidth) / _childWidth));
            int row = slot.Index / itemsPerRow;
            double top = row * _childHeight;
            double bottom = top + _childHeight;

            if (top < _offset.Y)
                SetVerticalOffset(top);
            else if (bottom > _offset.Y + ViewportHeight)
                SetVerticalOffset(bottom - ViewportHeight);

            return new Rect(slot.Element.TranslatePoint(new Point(0, 0), this),
                new Size(_childWidth, _childHeight));
        }
        return Rect.Empty;
    }
}
