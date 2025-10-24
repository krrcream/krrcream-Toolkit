### 右下角菜单说明 (Right-bottom Menu Description)

该菜单位于界面右下角，提供以下功能选项：
This menu is located in the bottom-right corner of the interface and provides the following options:

- **主题 (Theme)**:  
  点击可切换软件的主题样式（如浅色/深色模式），用于调整界面视觉风格。  
  Click to switch the software's theme style (e.g., light/dark mode) to adjust the visual appearance.

- **背景效果 (Backdrop)**:  
  点击可设置或关闭界面的背景动态效果，提升视觉体验。  
  Click to enable or disable the background dynamic effects for enhanced visual experience.

- **系统调色 (Update Accent)**:  
  勾选后，软件将自动跟随操作系统的颜色主题（如 Windows 的深色/浅色模式）进行适配。  
  When checked, the software will automatically adapt to the system's color scheme (e.g., Windows light/dark mode).

- **English (中文)**:  
  切换界面语言为中文。当前显示为英文时，点击此选项可切换至中文界面。  
  Switch the interface language to Chinese. When currently displayed in English, clicking this option will switch to Chinese.


# 狂风插入 操作指南 (NtoN Guide)

## 参数设置 (Parameter Settings)

- **键数 (Keys)**:  
  控制转换后的键位数量，可通过滑块调整。  
  Controls the number of keys after conversion, adjustable via slider.

- **上限权重 (Max Keys)**:  
  每一行的最大物件数量，用于调整密度。  
  Maximum number of notes per line, used to adjust density.

- **下限权重 (Min Keys)**:  
  每一行的最低物件数量，对物件数量原本低于该值的行不生效，用于调整密度。  
  Minimum number of notes per line; lines originally with fewer notes are unaffected. Used to adjust density.

- **转换速度 (Transform Speed)**:  
  调整变换速度，决定节奏变化的频率。  
  Adjusts transformation speed, determining the frequency of rhythmic changes.

- **种子 (Seed)**:  
  输入种子值用于控制随机生成的稳定性，相同种子可复现相同结果。  
  Enter a seed value to control the stability of random generation; identical seeds produce identical results.

### 过滤选项（Filter）

启用或禁用对应原始键数谱面的过滤，如果全不选则不开启过滤。  
Enable or disable filtering for maps with specific original key counts. If none are selected, filtering is disabled.

### 预设（Presets）

- **Default**:  
  应用默认配置。  
  Apply default settings.

- **NK预设 (NK Preset)**:  
  应用针对N键的预设配置。  
  Apply preset configuration for N-keys mode.

- **散至NK (ANK)**:  
  不增加物件数量散开变成N键模式。  
  Spread out without increasing note count to N-keys mode.

- **降至NK (DownToNK)**:  
  高键数谱面降级至 N键模式。  
  Downgrade high-keys maps to N-keys mode.

- **保存为预设 (Save as Preset)**:  
  保存设置为预设。  
  Save current settings as a preset.

------
# DP工具 操作指南 (DP Tool Guide)

## 参数设置 (Parameter Settings)

- **键数 (Keys)**:  
  控制是否启用键数转换功能，关闭时默认原谱keys×2。  
  Controls whether keys×2 conversion is enabled. When disabled, the original key count is doubled by default.

- **左手 (Left Hand)**:
  - **镜像 (Mirror)**:  
    启用左手指法的镜像对称处理。  
    Enable mirror symmetry for left-hand patterns.
  - **密度 (Density)**:  
    调整左手物件分布的密度。开启后，下方滑条将生效。  
    Adjust the density of notes on the left hand. Enabling this option activates the slider below.

- **右手 (Right Hand)**:
  - **镜像 (Mirror)**:  
    启用右手指法的镜像对称处理。开启后，下方滑条将生效。  
    Enable mirror symmetry for right-hand patterns.
  - **密度 (Density)**:  
    调整右手物件分布的密度。  
    Adjust the density of notes on the right hand. Enabling this option activates the slider below.

- **上限权重 (Max Weight)**:  
  设置每行最大物件数量的权重，用于控制密度上限。  
  Sets the maximum note count weight per line, used to control upper density limit.

- **下限权重 (Min Weight)**:  
  设置每行最小物件数量的权重，低于此值的行不生效。  
  Sets the minimum note count weight per line; lines with fewer notes are unaffected.

------
# KRR 转面器 操作指南 (KRR LN Transformer Guide)

## 参数设置 (Parameter Settings)

- **长度阈值 (Length Threshold)**:  
  设置长面的判定阈值，超过该长度的物件将被视为长面。单位为节拍。  
  物件到该列下一个物件的可用时间，超过该长度的物件将被视为"长"面，否则为"短"面。  
  Sets the threshold for identifying long notes; notes longer than this value are considered "long" notes, otherwise "short". Measured in beats.  
  The available time from a note to the next one in the same column; if longer than this threshold, it's classified as a "long" note.

### 短面 (Short LN)

- **短面占比 (Short LN Percentage)**:  
  设置短面生成的比例。  
  Sets the percentage of short notes to generate.

- **短面长度 (Short Length)**:  
  定义短面的长度。单位为节拍。  
  Defines the length of short notes, measured in beats.

- **短面限制 (Short Limit)**:  
  限制同一行中短面的数量。  
  Limits the number of short notes per line.

- **短面随机 (Short Random)**:  
  控制短面长度的随机波动范围。  
  Controls the random variation range of short note lengths.

### 长面 (Long LN)

- **长面占比 (Long LN Percentage)**:  
  设置长面在总面中的比例。  
  Sets the proportion of long notes among all notes.

- **长面长度 (Long Length)**:  
  定义长面的长度，单位为百分比，为可用时间的百分比。  
  Defines the length of long notes as a percentage of the available time.

- **长面限制 (Long Limit)**:  
  限制一行中长面的数量。  
  Limits the number of long notes per line.

- **长面随机 (Long Random)**:  
  控制长面长度的随机波动范围。  
  Controls the random variation range of long note lengths.

- **对齐 N 节拍 (Align N Beat)**:  
  启用后，所有面的长度将对齐至 N 节拍的整数倍。  
  When enabled, all note lengths are aligned to integer multiples of N beats.

- **处理原始谱面 (Process Original)**:  
  是否对原谱中的面进行处理。  
  Whether to process original notes in the map.

- **判定难度 (OD)**:  
  勾选则修改OD。  
  When checked, modifies the Overall Difficulty (OD) value.

- **种子 (Seed)**:  
  输入种子值以控制随机生成排列，相同种子可复现相同结果。  
  Enter a seed value to control random generation; identical seeds produce identical results.

- **随机生成 (Generate)**:  
  点击生成按钮执行转换操作。  
  Click to execute the transformation process.

### 预设 (Presets)

- **默认 (Default)**:  
  应用默认配置。  
  Apply default settings.

- **反键 间隔1/4 (Inverse Space=1/4)**:  
  将反键间隔调整为 1/4 节拍。  
  Adjusts inverse key spacing to 1/4 beat.

- **反键 间隔1/2 (Inverse Space=1/2)**:  
  将反键间隔调整为 1/2 节拍。  
  Adjusts inverse key spacing to 1/2 beat.

- **放手 难 (Release H)**:  
  生成较难的放手。  
  Generates harder release patterns.

- **米变1/2面 (Note→1/2LN)**:  
  将普通音符转换为 1/2 节拍的长面。  
  Converts regular notes into long notes with 1/2 beat duration.

- **轻面 (Easy LN)**:  
  生成较为简单的面条模式。  
  Generates simple long note patterns.

- **中面 (Mid LN)**:  
  生成中等难度的面条模式。  
  Generates medium-difficulty long note patterns.

- **大面 (Hard LN)**:  
  生成高难度的面条模式。  
  Generates high-difficulty long note patterns.

---
# OSU Listener 操作指南

## 功能概述 (Overview)

`OSU Listener` 是 krrcream's Toolkit 中用于监听 osu! 客户端并实现快捷转换谱面的核心模块。通过该界面，用户可以设置歌曲目录、绑定快捷键，并实时预览或直接转换当前选中的谱面。
`OSU Listener` is the core module in krrcream's Toolkit for monitoring the osu! client and enabling quick beatmap transformation. Through this interface, users can set the song directory, bind hotkeys, and preview or directly transform the currently selected beatmap in real time.

## 参数设置 (Settings)

- **Browse Songs**:  
  点击按钮可选择 osu! 游戏的 `Songs` 目录路径，确保工具能正确读取谱面文件。  
  Selects the path to the osu! game's `Songs` folder, enabling the tool to access beatmaps.

- **N2NC Hotkey (Ctrl+Shift+N)**:  
  设置 NtoN Converter 的快捷键。按下后将对 osu! 中选中的谱面执行 NtoN 转换。
  - **红色框**：表示快捷键与系统或其他程序冲突，无法使用。 Indicates a hotkey conflict with the system or other programs; cannot be used.
  - **蓝色框**：表示快捷键可用，已成功绑定。 Indicates the hotkey is available and successfully bound.  
  
  Sets the hotkey for NtoN Converter. Pressing it will apply the NtoN transformation to the currently selected map in osu!.
  - **Red box**: Indicates a hotkey conflict with the system or other programs; cannot be used.
  - **Blue box**: Indicates the hotkey is available and successfully bound.
- **DP Hotkey (Ctrl+Shift+D)**:  
  设置 DP Tool 的快捷键。按下后将对选中谱面应用 DP 工具处理。
  - **红色框**：快捷键冲突。
  - **蓝色框**：快捷键可用。  
  
  Sets the hotkey for DP Tool. Pressing it will apply the DP transformation to the currently selected map in osu!.
  - **Red box**: Hotkey conflict.
  - **Blue box**: Hotkey available.
- **KRRLN Hotkey (Ctrl+Shift+K)**:  
  设置 KRR LN Transformer 的快捷键。按下后将对选中谱面进行长面条转换。
  - **红色框**：快捷键冲突。
  - **蓝色框**：快捷键可用。  
  
  Sets the hotkey for KRR LN Transformer. Pressing it will apply the KRRLN transformation to the currently selected map in osu!.
  - **Red box**: Hotkey conflict.
  - **Blue box**: Hotkey available.

## 实时功能 (Real-time Features)

- **Real-time preview (实时预览)**:  
  按下此按钮后，软件会自动从 osu! 客户端提取当前选中的谱面信息，并在对应转谱器标签页中显示转换效果。  
  可用于快速查看转换结果，无需手动导入。  
  Click to enable real-time extraction of the currently selected beatmap from osu!. The transformed result will be displayed in the corresponding converter tab without manual import.

## 使用说明 (Instructions)


1. 首先点击 `Browse Songs` 设置正确的 osu! 歌曲目录。  
   First, click `Browse Songs` to set the correct osu! song directory.
2. 为三个转谱器设置合适的快捷键（建议避免与其他软件冲突）。  
   Assign appropriate hotkeys for the three converters (recommended to avoid conflicts with other software).
3. 点击 `实时预览` 启用监听后，在 osu! 中选择任意谱面。  
   Click `Real-time preview` to enable listening, then select any beatmap in osu!.
4. 按下对应快捷键即可触发转换。  
   Press the corresponding hotkey to trigger the transformation.

> ⚠️ 注意：必须点击 `实时预览` 才能使用快捷键功能。  
> ⚠️ Note: Must click `Real-time preview` to enable hotkey functionality.
