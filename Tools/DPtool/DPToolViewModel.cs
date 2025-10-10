using System;
using System.ComponentModel;
using krrTools.Bindable;
using krrTools.Configuration;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// DP工具响应式ViewModel - 双侧键位转换配置的响应式管理
    /// 核心功能：实时配置更新 + 预览联动 + 智能约束处理
    /// 融合ToolViewModelBase基础功能和Bindable的响应式能力
    /// 架构与N2NCViewModel完全一致
    /// </summary>
    public class DPToolViewModel : ToolViewModelBase<DPToolOptions>, IPreviewOptionsProvider
    {
        // 响应式属性 - 核心配置项
        private Bindable<bool> _modifySingleSideKeyCount = null!;
        private Bindable<double> _singleSideKeyCount = null!;
        private Bindable<bool> _lMirror = null!;
        private Bindable<bool> _lDensity = null!;
        private Bindable<bool> _lRemove = null!;
        private Bindable<double> _lMaxKeys = null!;
        private Bindable<double> _lMinKeys = null!;
        private Bindable<bool> _rMirror = null!;
        private Bindable<bool> _rDensity = null!;
        private Bindable<bool> _rRemove = null!;
        private Bindable<double> _rMaxKeys = null!;
        private Bindable<double> _rMinKeys = null!;


        public DPToolViewModel(DPToolOptions options) : base(ConverterEnum.DP, true, options)
        {
            // 初始化响应式属性 - 配置双向绑定
            InitializeReactivePropertiesInternal();

            // 设置属性约束和联动关系
            SetupPropertyConstraints();
        }

        /// <summary>
        /// 内部响应式属性初始化 - 避免虚方法调用警告
        /// </summary>
        private void InitializeReactivePropertiesInternal()
        {
            // 创建响应式属性实例并绑定到Options
            _modifySingleSideKeyCount = new Bindable<bool>(Options.ModifySingleSideKeyCount);
            _singleSideKeyCount = new Bindable<double>(Options.SingleSideKeyCount);
            _lMirror = new Bindable<bool>(Options.LMirror);
            _lDensity = new Bindable<bool>(Options.LDensity);
            _lRemove = new Bindable<bool>(Options.LRemove);
            _lMaxKeys = new Bindable<double>(Options.LMaxKeys);
            _lMinKeys = new Bindable<double>(Options.LMinKeys);
            _rMirror = new Bindable<bool>(Options.RMirror);
            _rDensity = new Bindable<bool>(Options.RDensity);
            _rRemove = new Bindable<bool>(Options.RRemove);
            _rMaxKeys = new Bindable<double>(Options.RMaxKeys);
            _rMinKeys = new Bindable<double>(Options.RMinKeys);

            // 统一事件绑定：响应式属性 → Options对象 + 事件发布（避免多重订阅）
            _modifySingleSideKeyCount.OnValueChanged(value =>
            {
                Options.ModifySingleSideKeyCount = value;
            });
            _singleSideKeyCount.OnValueChanged(value =>
            {
                Options.SingleSideKeyCount = value;
            });
            // Bindable OnValueChanged: 负责 ViewModel → Options 数据同步
            _lMirror.OnValueChanged(value =>
            {
                Options.LMirror = value;
            });
            _lDensity.OnValueChanged(value =>
            {
                Options.LDensity = value;
            });
            _lRemove.OnValueChanged(value =>
            {
                Options.LRemove = value;
            });
            _lMaxKeys.OnValueChanged(value =>
            {
                Options.LMaxKeys = value;
            });
            _lMinKeys.OnValueChanged(value =>
            {
                Options.LMinKeys = value;
            });
            _rMirror.OnValueChanged(value =>
            {
                Options.RMirror = value;
            });
            _rDensity.OnValueChanged(value =>
            {
                Options.RDensity = value;
            });
            _rRemove.OnValueChanged(value =>
            {
                Options.RRemove = value;
            });
            _rMaxKeys.OnValueChanged(value =>
            {
                Options.RMaxKeys = value;
            });
            _rMinKeys.OnValueChanged(value =>
            {
                Options.RMinKeys = value;
            });

            // 反向绑定：Options → 响应式属性（当Options从外部改变时更新响应式属性）
            Options.PropertyChanged += OnOptionsPropertyChanged;

            Console.WriteLine("[DPToolViewModel] 响应式属性初始化完成");
        }
    
        /// <summary>
        /// 处理Options属性变化 - 同步到响应式属性
        /// </summary>
        private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.ModifySingleSideKeyCount):
                    if (_modifySingleSideKeyCount.Value != Options.ModifySingleSideKeyCount)
                    {
                        _modifySingleSideKeyCount.Value = Options.ModifySingleSideKeyCount;
                    }
                    break;
                case nameof(Options.SingleSideKeyCount):
                    if (Math.Abs(_singleSideKeyCount.Value - Options.SingleSideKeyCount) > 1e-8)
                    {
                        _singleSideKeyCount.Value = Options.SingleSideKeyCount;
                    }
                    break;
                case nameof(Options.LMirror):
                    if (_lMirror.Value != Options.LMirror)
                    {
                        _lMirror.Value = Options.LMirror;
                    }
                    break;
                case nameof(Options.LDensity):
                    if (_lDensity.Value != Options.LDensity)
                    {
                        _lDensity.Value = Options.LDensity;
                    }
                    break;
                case nameof(Options.LRemove):
                    if (_lRemove.Value != Options.LRemove)
                    {
                        _lRemove.Value = Options.LRemove;
                    }
                    break;
                case nameof(Options.LMaxKeys):
                    if (Math.Abs(_lMaxKeys.Value - Options.LMaxKeys) > 1e-8)
                    {
                        _lMaxKeys.Value = Options.LMaxKeys;
                    }
                    break;
                case nameof(Options.LMinKeys):
                    if (Math.Abs(_lMinKeys.Value - Options.LMinKeys) > 1e-8)
                    {
                        _lMinKeys.Value = Options.LMinKeys;
                    }
                    break;
                case nameof(Options.RMirror):
                    if (_rMirror.Value != Options.RMirror)
                    {
                        _rMirror.Value = Options.RMirror;
                    }
                    break;
                case nameof(Options.RDensity):
                    if (_rDensity.Value != Options.RDensity)
                    {
                        _rDensity.Value = Options.RDensity;
                    }
                    break;
                case nameof(Options.RRemove):
                    if (_rRemove.Value != Options.RRemove)
                    {
                        _rRemove.Value = Options.RRemove;
                    }
                    break;
                case nameof(Options.RMaxKeys):
                    if (Math.Abs(_rMaxKeys.Value - Options.RMaxKeys) > 1e-8)
                    {
                        _rMaxKeys.Value = Options.RMaxKeys;
                    }
                    break;
                case nameof(Options.RMinKeys):
                    if (Math.Abs(_rMinKeys.Value - Options.RMinKeys) > 1e-8)
                    {
                        _rMinKeys.Value = Options.RMinKeys;
                    }
                    break;
            }
        }

        // 防循环标志
        private bool _isUpdatingConstraints;

        /// <summary>
        /// 设置属性约束和智能联动 - 业务逻辑核心（防循环调用）
        /// </summary>
        private void SetupPropertyConstraints()
        {
            // 智能约束1: LMaxKeys变更时确保LMinKeys不超过它
            _lMaxKeys.OnValueChanged(maxKeys =>
            {
                if (!_isUpdatingConstraints && _lMinKeys.Value > maxKeys)
                {
                    Console.WriteLine($"[DP约束] LMaxKeys变更 {maxKeys}，自动调整LMinKeys: {_lMinKeys.Value} → {maxKeys}");
                    _isUpdatingConstraints = true;
                    _lMinKeys.Value = maxKeys;
                    _isUpdatingConstraints = false;
                }
            });

            // 智能约束2: LMinKeys变更时确保不超过LMaxKeys
            _lMinKeys.OnValueChanged(minKeys =>
            {
                if (!_isUpdatingConstraints && minKeys > _lMaxKeys.Value)
                {
                    Console.WriteLine($"[DP约束] LMinKeys > LMaxKeys，自动调整: {minKeys} → {_lMaxKeys.Value}");
                    _isUpdatingConstraints = true;
                    _lMinKeys.Value = _lMaxKeys.Value;
                    _isUpdatingConstraints = false;
                }
            });

            // 智能约束3: RMaxKeys变更时确保RMinKeys不超过它  
            _rMaxKeys.OnValueChanged(maxKeys =>
            {
                if (!_isUpdatingConstraints && _rMinKeys.Value > maxKeys)
                {
                    Console.WriteLine($"[DP约束] RMaxKeys变更 {maxKeys}，自动调整RMinKeys: {_rMinKeys.Value} → {maxKeys}");
                    _isUpdatingConstraints = true;
                    _rMinKeys.Value = maxKeys;
                    _isUpdatingConstraints = false;
                }
            });

            // 智能约束4: RMinKeys变更时确保不超过RMaxKeys
            _rMinKeys.OnValueChanged(minKeys =>
            {
                if (!_isUpdatingConstraints && minKeys > _rMaxKeys.Value)
                {
                    Console.WriteLine($"[DP约束] RMinKeys > RMaxKeys，自动调整: {minKeys} → {_rMaxKeys.Value}");
                    _isUpdatingConstraints = true;
                    _rMinKeys.Value = _rMaxKeys.Value;
                    _isUpdatingConstraints = false;
                }
            });

            // 注意：事件发布已合并到 InitializeReactivePropertiesInternal 中，避免重复订阅
        }



        // 公开属性 - 响应式架构，与N2NC保持一致

        public bool ModifySingleSideKeyCount
        {
            get => _modifySingleSideKeyCount.Value;
            set
            {
                if (_modifySingleSideKeyCount.Value != value)
                {
                    _modifySingleSideKeyCount.Value = value;
                    OnPropertyChanged();
                }
            }
        }

        public double SingleSideKeyCount
        {
            get => _singleSideKeyCount.Value;
            set
            {
                if (Math.Abs(_singleSideKeyCount.Value - value) > 1e-8)
                {
                    _singleSideKeyCount.Value = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool LMirror
        {
            get => _lMirror.Value;
            set
            {
                if (_lMirror.Value != value)
                {
                    Console.WriteLine($"[DP数据流] LMirror变更: {_lMirror.Value} → {value}");
                    _lMirror.Value = value;
                    OnPropertyChanged(); // UI通知
                    // 注意：Options更新和事件发布由Bindable.OnValueChanged处理
                }
            }
        }

        public bool LDensity
        {
            get => _lDensity.Value;
            set
            {
                if (_lDensity.Value != value)
                {
                    _lDensity.Value = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool LRemove
        {
            get => _lRemove.Value;
            set
            {
                if (_lRemove.Value != value)
                {
                    _lRemove.Value = value;
                    OnPropertyChanged(); // 恢复调用，与N2NC保持一致
                }
            }
        }

        public double LMaxKeys
        {
            get => _lMaxKeys.Value;
            set
            {
                if (Math.Abs(_lMaxKeys.Value - value) > 1e-8)
                {
                    _lMaxKeys.Value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LMinKeysMaximum)); // 通知计算属性更新
                }
            }
        }

        public double LMinKeys
        {
            get => _lMinKeys.Value;
            set
            {
                if (Math.Abs(_lMinKeys.Value - value) > 1e-8)
                {
                    _lMinKeys.Value = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RMirror
        {
            get => _rMirror.Value;
            set
            {
                if (_rMirror.Value != value)
                {
                    Console.WriteLine($"[DP数据流] RMirror变更: {_rMirror.Value} → {value}");
                    _rMirror.Value = value;
                    OnPropertyChanged(); // UI通知
                    // 注意：Options更新和事件发布由Bindable.OnValueChanged处理
                }
            }
        }

        public bool RDensity
        {
            get => _rDensity.Value;
            set
            {
                if (_rDensity.Value != value)
                {
                    _rDensity.Value = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RRemove
        {
            get => _rRemove.Value;
            set
            {
                if (_rRemove.Value != value)
                {
                    _rRemove.Value = value;
                    OnPropertyChanged(); // 恢复调用，与N2NC保持一致
                }
            }
        }

        public double RMaxKeys
        {
            get => _rMaxKeys.Value;
            set
            {
                if (Math.Abs(_rMaxKeys.Value - value) > 1e-8)
                {
                    _rMaxKeys.Value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RMinKeysMaximum)); // 通知计算属性更新
                }
            }
        }

        public double RMinKeys
        {
            get => _rMinKeys.Value;
            set
            {
                if (Math.Abs(_rMinKeys.Value - value) > 1e-8)
                {
                    _rMinKeys.Value = value;
                    OnPropertyChanged();
                }
            }
        }

        public IToolOptions GetPreviewOptions()
        {
            return new DPToolOptions
            {
                ModifySingleSideKeyCount = Options.ModifySingleSideKeyCount,
                SingleSideKeyCount = Options.SingleSideKeyCount,
                LMirror = Options.LMirror,
                LDensity = Options.LDensity,
                LRemove = Options.LRemove,
                LMaxKeys = Options.LMaxKeys,
                LMinKeys = Options.LMinKeys,
                RMirror = Options.RMirror,
                RDensity = Options.RDensity,
                RRemove = Options.RRemove,
                RMaxKeys = Options.RMaxKeys,
                RMinKeys = Options.RMinKeys
            };
        }

        // 计算属性 - 动态最大值约束
        public double LMinKeysMaximum => LMaxKeys;
        public double RMinKeysMaximum => RMaxKeys;

        /// <summary>
        /// 释放资源，取消所有事件订阅
        /// </summary>
        public new void Dispose()
        {
            // 取消Options属性变化的事件订阅
            Options.PropertyChanged -= OnOptionsPropertyChanged;
            base.Dispose();
        }
    }
}