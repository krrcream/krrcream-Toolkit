# 转换模块统一重构总结

## 重构目标
将原本差异较大的三个转换器（N2N、DP、KRRLN）统一到一个标准化的模块架构中，实现：
- 统一的设置管理
- 标准化的UI构建
- 一致的转换逻辑接口
- 模块化的注册和发现系统

## 核心架构

### 1. 模块枚举 (`ToolModuleType`)
```csharp
public enum ToolModuleType
{
    N2NC,
    DP,
    KRRLN,
    // 新模块只需在此添加
}
```

### 2. 统一选项基类 (`UnifiedToolOptions`)
- 继承 `ToolOptionsBase`
- 包含通用设置：`SelectedPreset`
- 各模块选项类继承此基类

### 3. 模块基类 (`ToolModuleBase`)
- 泛型抽象类：`ToolModuleBase<TOptions, TViewModel, TControl>`
- 实现 `IToolModule` 接口
- 提供标准化的创建方法

### 4. 模块注册表 (`ToolModuleRegistry`)
- 静态注册所有模块
- 提供模块发现和访问接口

## 具体模块实现

### N2NC模块 (`N2NCModule`)
- 选项：`N2NCOptions` (继承 `UnifiedToolOptions`)
- ViewModel：`N2NCViewModel` (继承 `ToolViewModelBase<N2NCOptions>`)
- Control：`N2NCControl` (继承 `ToolControlBase<N2NCOptions>`)
- 核心算法：`N2NCService.ProcessBeatmap`

### DP模块 (`DPModule`)
- 选项：`DPToolOptions` (继承 `UnifiedToolOptions`)
- ViewModel：`DPToolViewModel` (继承 `ToolViewModelBase<DPToolOptions>`)
- Control：`DPToolControl` (继承 `ToolControlBase<DPToolOptions>`)
- 核心算法：`DP.DPBeatmapToData`

### KRRLN模块 (`KRRLNModule`)
- 选项：`KRRLNTransformerOptions` (继承 `UnifiedToolOptions`)
- ViewModel：`KRRLNTransformerViewModel` (继承 `ToolViewModelBase<KRRLNTransformerOptions>`)
- Control：`KRRLNTransformerControl` (继承 `ToolControlBase<KRRLNTransformerOptions>`)
- 核心算法：`KRRLN.ProcessBeatmapToData`

## 新模块创建流程

要添加新模块，只需：

1. **在 `ToolModuleType` 枚举中添加新项**
   ```csharp
   public enum ToolModuleType
   {
       N2NC,
       DP,
       KRRLN,
       NewModule,  // 添加新项
   }
   ```

2. **创建选项类**
   ```csharp
   public class NewModuleOptions : UnifiedToolOptions
   {
       // 模块特定设置
       public double SomeSetting { get; set; }
       public override void Validate() { /* 验证逻辑 */ }
   }
   ```

3. **创建ViewModel**
   ```csharp
   public class NewModuleViewModel : ToolViewModelBase<NewModuleOptions>
   {
       public NewModuleViewModel() : base("NewModuleToolName") { }
   }
   ```

4. **创建Control**
   ```csharp
   public class NewModuleControl : ToolControlBase<NewModuleOptions>
   {
       public NewModuleControl() : base("NewModuleToolName") { }
       // 实现UI构建
   }
   ```

5. **创建核心算法类**
   ```csharp
   public class NewModuleProcessor
   {
       public static Beatmap Process(Beatmap input, NewModuleOptions options)
       {
           // 核心转换逻辑
       }
   }
   ```

6. **创建模块类**
   ```csharp
   public class NewModule : ToolModuleBase<NewModuleOptions, NewModuleViewModel, NewModuleControl>
   {
       public override ToolModuleType ModuleType => ToolModuleType.NewModule;
       public override string ModuleName => "NewModuleToolName";
       public override string DisplayName => "New Module";

       public override Beatmap ProcessBeatmap(Beatmap input, NewModuleOptions options)
       {
           return NewModuleProcessor.Process(input, options);
       }
   }
   ```

7. **在注册表中注册**
   ```csharp
   static ToolModuleRegistry()
   {
       RegisterModule(new N2NCModule());
       RegisterModule(new DPModule());
       RegisterModule(new KRRLNModule());
       RegisterModule(new NewModule());  // 注册新模块
   }
   ```

## 优势

1. **代码复用**：通用逻辑统一管理，减少重复代码
2. **类型安全**：编译时检查模块一致性
3. **易维护**：新模块只需实现特定部分
4. **自动化集成**：无需手动修改多个文件
5. **扩展性**：支持动态加载模块

## 兼容性
- 保持了与现有UI和配置系统的兼容性
- MainWindow中的初始化逻辑保持不变
- 现有的工具发现和使用方式保持不变

## 后续优化
1. 完善GenericTool的实现，支持完整的文件处理流程
2. 添加模块的动态加载支持
3. 实现统一的预设管理系统
4. 添加模块的插件化支持</content>
<parameter name="filePath">e:\BASE CODE\GitHub\krrcream-Toolkit\MODULE_REFACTOR_SUMMARY.md