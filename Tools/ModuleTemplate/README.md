# 转换模块模板

此文件夹包含创建新转换模块所需的模板文件。

## 使用步骤

### 1. 复制模板文件
将此文件夹复制到一个新文件夹，命名为你的模块名称（例如：`MyNewModule`）。

### 2. 重命名和修改文件
- 将所有 `.template` 文件重命名为 `.cs` 文件
- 将文件中的所有 `ModuleName` 替换为你的实际模块名称
- 修改相应的命名空间、类名和属性

### 3. 修改枚举
在 `ToolModuleType` 枚举中添加新模块：
```csharp
public enum ToolModuleType
{
    N2NC,
    DP,
    KRRLN,
    MyNewModule,  // 添加这一行
}
```

### 4. 注册模块
在 `ToolModuleRegistry.cs` 的静态构造函数中注册新模块：
```csharp
static ToolModuleRegistry()
{
    RegisterModule(new N2NCModule());
    RegisterModule(new DPModule());
    RegisterModule(new KRRLNModule());
    RegisterModule(new MyNewModule());  // 添加这一行
}
```

### 5. 实现核心算法
在 `Processor.cs` 中实现你的转换逻辑。

### 6. 配置UI
在 `Control.cs` 中构建你的用户界面。

### 7. 添加选项
在 `Options.cs` 中定义你的模块设置。

## 文件说明

- `Module.cs.template` - 模块主类，实现模块接口
- `Options.cs.template` - 模块选项类，定义设置
- `ViewModel.cs.template` - 视图模型类，处理数据绑定
- `Control.cs.template` - 用户界面控件类
- `Processor.cs.template` - 核心算法处理类

## 模板文件中的占位符

- `ModuleName` - 替换为你的模块名称（如：`MyConverter`）
- `TODO` - 表示需要实现的代码部分

## 注意事项

1. 所有类名中的 `ModuleName` 都需要替换为实际名称
2. 确保命名空间正确
3. 实现 `ProcessBeatmap` 方法作为核心转换逻辑
4. 在UI中添加适当的输入验证
5. 测试模块的保存和加载功能

## 快速开始脚本

项目根目录提供了 `copy_module_template.bat` 脚本，可以自动复制和重命名模板文件：

```cmd
copy_module_template.bat MyNewModule
```

此脚本会：
1. 创建 `tools\MyNewModule\` 文件夹
2. 复制所有模板文件并重命名为 `.cs` 文件
3. 自动替换文件中的 `ModuleName` 为 `MyNewModule`

## 手动步骤

如果不使用脚本，请按以下步骤操作：

假设你要创建名为 `SpeedChanger` 的模块：

1. 创建 `tools\SpeedChanger\` 文件夹
2. 复制所有模板文件到该文件夹
3. 重命名文件并替换内容：
   - `ModuleName` → `SpeedChanger`
   - `ModuleNameTool` → `SpeedChangerTool`
   - 实现具体的转换逻辑