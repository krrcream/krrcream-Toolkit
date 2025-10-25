using System;
using System.ComponentModel;
using System.Windows.Threading;
using krrTools.Bindable;
using krrTools.Configuration;

namespace krrTools.Utilities
{
    /// <summary>
    /// 全局状态管理器 - 使用Bindable系统统一管理应用状态
    /// 实现驱动式响应和统一状态管理
    /// </summary>
    public class StateBarManager : INotifyPropertyChanged
    {
        [Inject]
        private IEventBus EventBus { get; set; } = null!;

        private bool _isTopmost;

        // 进度条自动隐藏定时器
        private readonly DispatcherTimer? _progressHideTimer;

        // 核心状态 - 直接使用全局设置
        public Bindable<bool> IsOsuRunning { get; } = new Bindable<bool>();
        public Bindable<string> CurrentBeatmapPath { get; } = new Bindable<string>(string.Empty);
        public Bindable<ListenerState> ListenerStateBindable { get; } = new Bindable<ListenerState>();
        public Bindable<bool> IsPlaying { get; } = new Bindable<bool>();
        public Bindable<bool> IsFrozen { get; } = new Bindable<bool>();
        public Bindable<bool> IsHidden { get; } = new Bindable<bool>();
        public Bindable<double> ProgressValue { get; } = new Bindable<double>();
        public Bindable<bool> ProgressVisible { get; } = new Bindable<bool>();

        public StateBarManager()
        {
            // 自动注入标记了 [Inject] 的属性
            this.InjectServices();

            // 初始化进度条隐藏定时器
            _progressHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _progressHideTimer.Tick += (_, _) =>
            {
                ProgressVisible.Value = false;
                _progressHideTimer.Stop();
            };

            // 设置状态绑定和响应逻辑
            SetupStateBindings();
            EventBus.Subscribe<MonitoringEnabledChangedEvent>(OnMonitoringEnabledChanged);
        }

        /// <summary>
        /// 设置置顶状态
        /// </summary>
        public void SetTopmost(bool value)
        {
            _isTopmost = value;
            // 检查隐藏逻辑
            if (IsPlaying.Value && value)
                IsHidden.Value = true;
            else if (!value) IsHidden.Value = false;
        }

        #region 公共属性

        /// <summary>
        /// 实时预览是否启用 - 直接从全局设置读取
        /// </summary>
        public bool IsMonitoringEnable
        {
            get => BaseOptionsManager.GetRealTimePreview();
            set => BaseOptionsManager.SetMonitoring(value);
        }

        #endregion

        #region 状态绑定设置

        private void SetupStateBindings()
        {
            // osu!运行状态变化时，更新监听器状态
            IsOsuRunning.OnValueChanged(running =>
            {
                EventBus.Publish(new OsuRunningEvent
                {
                    OldValue = !running,
                    NewValue = running
                });

                if (IsMonitoringEnable)
                {
                    ListenerStateBindable.Value = running
                                                      ? ListenerState.Monitoring
                                                      : ListenerState.WaitingForOsu;
                }

                OnPropertyChanged(nameof(IsOsuRunning));
            });

            // // 当前谱面路径变化时，发布事件
            // CurrentBeatmapPath.OnValueChanged(path =>
            // {
            //     // 检查路径是否与全局设置中的最后预览路径不同
            //     var globalSettings = BaseOptionsManager.GetGlobalSettings();
            //     if (!string.IsNullOrEmpty(path) && path != globalSettings.LastPreviewPath.Value)
            //     {
            //         // 保存到全局设置
            //         BaseOptionsManager.UpdateGlobalSettings(settings => settings.LastPreviewPath.Value = path);
            //
            //         // 发布事件
            //         EventBus.Publish(new BeatmapChangedEvent
            //         {
            //             FilePath = path,
            //             FileName = System.IO.Path.GetFileName(path),
            //             ChangeType = BeatmapChangeType.FromMonitoring,
            //         });
            //     }
            //
            // });

            // 监听器状态变化时，触发属性变更通知
            ListenerStateBindable.OnValueChanged(_ => { OnPropertyChanged(nameof(ListenerStateBindable)); });

            // IsPlaying变化时，处理隐藏和冻结逻辑
            IsPlaying.OnValueChanged(playing =>
            {
                // 隐藏逻辑：只有置顶开启时才隐藏
                if (playing && _isTopmost)
                    IsHidden.Value = true;
                else if (!playing) IsHidden.Value = false;

                // 冻结逻辑：只有监听开启时才冻结
                if (playing && IsMonitoringEnable)
                    IsFrozen.Value = true;
                else if (!playing) IsFrozen.Value = false;

                OnPropertyChanged(nameof(IsPlaying));
            });

            // IsFrozen变化时，触发属性变更通知
            IsFrozen.OnValueChanged(_ => { OnPropertyChanged(nameof(IsFrozen)); });

            // IsHidden变化时，触发属性变更通知
            IsHidden.OnValueChanged(_ => { OnPropertyChanged(nameof(IsHidden)); });

            // ProgressValue变化时，自动控制ProgressVisible
            ProgressValue.OnValueChanged(value =>
            {
                if (value >= 100)
                {
                    // 进度完成，先显示进度条，然后3秒后自动隐藏
                    ProgressVisible.Value = true;
                    if (_progressHideTimer is { IsEnabled: false }) _progressHideTimer.Start();
                }
                else if (value > 0)
                {
                    // 进度进行中，显示进度条
                    ProgressVisible.Value = true;
                    // 如果定时器正在运行，停止它
                    if (_progressHideTimer is { IsEnabled: true }) _progressHideTimer.Stop();
                }
                else
                {
                    // 进度为0，隐藏进度条
                    ProgressVisible.Value = false;
                    // 停止定时器
                    if (_progressHideTimer is { IsEnabled: true }) _progressHideTimer.Stop();
                }
            });
        }

        #endregion

        #region 事件订阅

        private void OnMonitoringEnabledChanged(MonitoringEnabledChangedEvent evt)
        {
            // 使用Dispatcher确保在UI线程上执行，避免跨线程访问错误
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // 当监控启用状态改变时，根据当前 osu! 运行状态更新监听器状态
                if (evt.NewValue)
                {
                    ListenerStateBindable.Value = IsOsuRunning.Value
                                                      ? ListenerState.Monitoring
                                                      : ListenerState.WaitingForOsu;
                }
                else
                {
                    ListenerStateBindable.Value = 0;
                    // 监听关闭时，重置预览到内置样本
                    EventBus.Publish(new ConvPrevRefreshOnlyEvent { NewValue = false });
                }

                // 检查冻结逻辑
                if (IsPlaying.Value && evt.NewValue)
                    IsFrozen.Value = true;
                else if (!evt.NewValue) IsFrozen.Value = false;
            });
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #region 状态枚举和事件类

    public enum ListenerState
    {
        Idle,
        Monitoring,
        WaitingForOsu,
        Stopped
    }

    #endregion
}
