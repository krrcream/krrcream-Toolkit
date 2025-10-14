using System;
using System.ComponentModel;
using krrTools.Bindable;
using krrTools.Configuration;

namespace krrTools.Utilities
{
    /// <summary>
    /// 全局状态管理器 - 使用Bindable系统统一管理应用状态
    /// 实现驱动式响应和统一状态管理
    /// </summary>
    public class StateBarManager : INotifyPropertyChanged, IDisposable
    {
        [Inject]
        private IEventBus EventBus { get; set; } = null!;

        // 跟踪上一次的状态，用于事件发布
        private bool _lastIsOsuRunning;

        // 核心状态 - 直接使用全局设置
        public Bindable<bool> IsOsuRunning { get; } = new();
        public Bindable<string> CurrentBeatmapPath { get; } = new(string.Empty);
        public Bindable<ListenerState> ListenerState { get; } = new();

        public StateBarManager()
        {
            // 自动注入标记了 [Inject] 的属性
            this.InjectServices();

            // 设置状态绑定和响应逻辑
            SetupStateBindings();
            EventBus.Subscribe<BeatmapChangedEvent>(OnBeatmapDetected);
            EventBus.Subscribe<MonitoringEnabledChangedEvent>(OnMonitoringEnabledChanged);
        }

        #region 公共属性

        /// <summary>
        /// 实时预览是否启用 - 直接从全局设置读取
        /// </summary>
        public bool IsMonitoringEnable
        {
            get => BaseOptionsManager.GetRealTimePreview();
            set =>
                // 直接设置全局设置，全局设置会发布事件
                BaseOptionsManager.SetMonitoring(value);
        }

        #endregion
        
        #region 状态绑定设置
        private void SetupStateBindings()
        {
            // 初始化上一次的值
            _lastIsOsuRunning = IsOsuRunning.Value;

            // osu!运行状态变化时，更新监听器状态
            IsOsuRunning.OnValueChanged(running =>
            {
                EventBus.Publish(new OsuRunningEvent
                {
                    OldValue = _lastIsOsuRunning,
                    NewValue = running,
                });
                _lastIsOsuRunning = running;
                
                if (IsMonitoringEnable)
                {
                    ListenerState.Value = running 
                        ? (ListenerState)1
                        : (ListenerState)2;
                }

                OnPropertyChanged(nameof(IsOsuRunning));
            });

            // 当前谱面路径变化时，发布事件
            CurrentBeatmapPath.OnValueChanged(path =>
            {
                // 检查路径是否与全局设置中的最后预览路径不同
                var globalSettings = BaseOptionsManager.GetGlobalSettings();
                if (!string.IsNullOrEmpty(path) && path != globalSettings.LastPreviewPath.Value)
                {
                    // 保存到全局设置
                    BaseOptionsManager.UpdateGlobalSettings(settings => settings.LastPreviewPath.Value = path);

                    // 发布事件
                    EventBus.Publish(new BeatmapChangedEvent
                    {
                        FilePath = path,
                        FileName = System.IO.Path.GetFileName(path),
                        ChangeType = BeatmapChangeType.FromMonitoring,
                    });
                }

            });

            // 监听器状态变化时，触发属性变更通知
            ListenerState.OnValueChanged(_ => { OnPropertyChanged(nameof(ListenerState)); });
        }

        #endregion

        #region 事件订阅
        private void OnBeatmapDetected(BeatmapChangedEvent evt)
        {
            // 更新全局设置中的最后预览路径
            BaseOptionsManager.UpdateGlobalSettings(settings => settings.LastPreviewPath.Value = evt.FilePath);
        }

        private void OnMonitoringEnabledChanged(MonitoringEnabledChangedEvent evt)
        {
            // 当监控启用状态改变时，根据当前 osu! 运行状态更新监听器状态
            if (evt.NewValue)
            {
                ListenerState.Value = IsOsuRunning.Value ? (ListenerState)1 : (ListenerState)2;
            }
            else
            {
                ListenerState.Value = 0;
                // 监听关闭时，重置预览到内置样本
                EventBus.Publish(new PreviewRefreshEvent { NewValue = false });
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            // 清理绑定和订阅
            // 注意：当前的 Bindable 实现不支持移除回调，这里只是为了符合接口
            // 在实际使用中，这些对象应该由垃圾回收器处理
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