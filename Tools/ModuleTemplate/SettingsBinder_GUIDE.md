# SettingsBinder 使用指南

`SettingsBinder` 是项目中用于自动生成UI控件的核心工具类，它通过反射和属性特性自动创建与数据模型绑定的用户界面控件。

## 核心特性

- **声明式UI生成**：通过 `[Option]` 特性声明UI控件，无需手动创建
- **自动数据绑定**：自动处理 `Bindable<T>` 类型的双向绑定
- **本地化支持**：自动从 `Strings` 类获取标签和提示文本
- **类型安全**：使用强类型表达式选择器，避免运行时错误

## OptionAttribute 属性

### 基本属性
- `LabelKey`: 本地化标签键名（必需）
- `TooltipKey`: 本地化提示文本键名（可选）
- `UIType`: UI控件类型（必需）
- `DataType`: 数据类型（可选，自动推断）

### 数值控件属性
- `Min`: 最小值
- `Max`: 最大值
- `TickFrequency`: 刻度频率（默认1）
- `KeyboardStep`: 键盘步长（默认1）

### 高级属性
- `DisplayMapField`: 显示值映射字典字段名
- `ActualMapField`: 实际值映射字典字段名
- `HasCheckBox`: 是否启用勾选框
- `CheckBoxProperty`: 勾选框绑定属性名
- `IsRefresher`: 是否触发预览刷新

## UIType 枚举

```csharp
public enum UIType
{
    Toggle,     // CheckBox - 布尔值切换
    Slider,     // Slider - 数值滑块
    Text,       // TextBox - 文本输入
    NumberBox,  // TextBox - 数值输入框
    ComboBox    // ComboBox - 下拉选择（枚举）
}
```

## 主要方法

### 1. CreateTemplatedControl

```csharp
public static FrameworkElement CreateTemplatedControl<T>(
    T options,
    Expression<Func<T, object>> propertySelector)
```

**用途**：根据属性的 `[Option]` 特性自动创建单个UI控件。

**示例**：
```csharp
// 在Options类中定义属性
[Option(LabelKey = "MySettingLabel", UIType = UIType.Slider, Min = 1, Max = 10)]
public Bindable<double> MySetting { get; } = new Bindable<double>(5);

// 在View中创建控件
var slider = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.MySetting);
```

### 2. CreateTemplatedSlider

```csharp
public static UIElement CreateTemplatedSlider<T>(
    T options,
    Expression<Func<T, object>> propertySelector,
    Expression<Func<T, object>>? checkPropertySelector = null,
    Dictionary<double, string>? valueDisplayMap = null)
```

**用途**：创建增强的滑块控件，支持可选的勾选框和值显示映射。

**特性**：
- 自动检测可空类型并添加勾选框
- 支持自定义显示值映射
- 支持属性级别的显示映射（通过 `DisplayMapField`）

**示例**：
```csharp
// 带显示映射的滑块
var speedSlider = SettingsBinder.CreateTemplatedSlider(
    _viewModel.Options,
    o => o.TransformSpeed,
    null,
    TransformSpeedDict);

// 自动勾选框（可空类型）
// 在定义为可空类型时自动添加勾选框UI，如果定义了具体的默认值，则视为默认勾选
[Option(LabelKey = "OptionalSetting", UIType = UIType.Slider, Min = 0, Max = 100)]
public Bindable<double> OptionalSetting { get; } = new Bindable<double>(); // 无勾选框
public Bindable<double?> OptionalSetting { get; } = new Bindable<double?>(); // 有勾选框，默认不勾选
public Bindable<double?> OptionalSetting { get; } = new Bindable<double?>(0); // 有勾选框，默认勾选
```

### 3. CreateTemplatedSliderWithDynamicMax

```csharp
public static UIElement CreateTemplatedSliderWithDynamicMax<T>(
    T options,
    Expression<Func<T, object>> propertySelector,
    object dynamicMaxSource,
    string dynamicMaxPath,
    Expression<Func<T, object>>? checkPropertySelector = null,
    Dictionary<double, string>? valueDisplayMap = null)
```

**用途**：创建最大值动态变化的滑块控件。

**示例**：
```csharp
// 最大值绑定到另一个属性的滑块
var minKeysSlider = SettingsBinder.CreateTemplatedSliderWithDynamicMax(
    _viewModel.Options,
    o => o.MinKeys,
    _viewModel,
    nameof(_viewModel.MinKeysMaximum));
```
**MinKeysMaximum是一个Dictionary<double, string>字典，映射：值→显示文本**

### 4. CreateSeedPanel

```csharp
public static FrameworkElement CreateSeedPanel<T, TProperty>(
    T dataContext,
    Expression<Func<T, TProperty>> seedProperty)
```

**用途**：创建种子值输入面板，包含标签、文本框和随机生成按钮。

**示例**：
```csharp
var seedPanel = SettingsBinder.CreateSeedPanel(_viewModel, x => x.Seed);
```

## 使用模式

### 1. 基本选项定义

```csharp
public class MyOptions : ToolOptionsBase
{
    [Option(LabelKey = "TargetKeysLabel", Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double), IsRefresher = true)]
    public Bindable<double> TargetKeys { get; } = new Bindable<double>(10);

    [Option(LabelKey = "EnableFeatureLabel", UIType = UIType.Toggle)]
    public Bindable<bool> EnableFeature { get; } = new Bindable<bool>(true);

    [Option(LabelKey = "DescriptionLabel", UIType = UIType.Text)]
    public Bindable<string> Description { get; } = new Bindable<string>("Default");
}
```

### 2. 带显示映射的选项

```csharp
public class MyOptions : ToolOptionsBase
{
    [Option(LabelKey = "SpeedLabel", Min = 1, Max = 8, UIType = UIType.Slider,
            DisplayMapField = nameof(SpeedDisplayDict), DataType = typeof(double), IsRefresher = true)]
    public Bindable<double> Speed { get; } = new Bindable<double>(5);

    public static readonly Dictionary<double, string> SpeedDisplayDict = new()
    {
        { 1, "1/16" }, { 2, "1/8" }, { 3, "1/4" }, { 4, "1/2" },
        { 5, "1" }, { 6, "2" }, { 7, "4" }, { 8, "8" }
    };
}
```

### 3. 可选值（带勾选框）

```csharp
public class MyOptions : ToolOptionsBase
{
    [Option(LabelKey = "OptionalValueLabel", Min = 0, Max = 100, UIType = UIType.Slider)]
    public Bindable<double?> OptionalValue { get; } = new Bindable<double?>(null);
}
```

### 4. 在View中使用

```csharp
public class MyView : ToolViewBase<MyOptions>
{
    public MyView() : base(ConverterEnum.MyConverter)
    {
        var grid = new StackPanel();

        // 添加各种控件
        grid.Children.Add(SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.TargetKeys));
        grid.Children.Add(SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.EnableFeature));
        grid.Children.Add(SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.Description));
        grid.Children.Add(SettingsBinder.CreateSeedPanel(_viewModel, x => x.Seed));

        Content = new ScrollViewer { Content = grid };
    }
}
```

## 最佳实践

1. **始终使用 Bindable<T>**：确保响应式数据绑定
2. **正确设置 IsRefresher**：只有影响转换结果的设置才设为true
3. **使用本地化键**：所有用户可见文本都应本地化
4. **合理使用显示映射**：为枚举值或特殊数值提供用户友好的显示
5. **测试数据绑定**：确保UI变化能正确更新数据模型

## 注意事项

- `SettingsBinder` 依赖于 `[Option]` 特性，缺少特性会导致创建失败
- 表达式选择器必须是有效的属性访问表达式
- `DataType` 通常可以自动推断，除非需要特殊处理
- 动态最大值滑块需要确保 `dynamicMaxPath` 路径有效</content>
<parameter name="filePath">e:\BASE CODE\GitHub\krrTools\Tools\ModuleTemplate\SettingsBinder_GUIDE.md