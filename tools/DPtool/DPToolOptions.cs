using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace krrTools.tools.DPtool
{
    /// <summary>
    /// DP工具选项类，用于封装所有DP参数
    /// </summary>
    public class DPToolOptions : INotifyPropertyChanged
    {
        private bool _modifySingleSideKeyCount = false;
        private int _singleSideKeyCount = 5;
        
        private bool _lMirror = false;
        private bool _lDensity = false;
        private int _lMaxKeys = 5;
        private int _lMinKeys = 0;
        
        private bool _rMirror = false;
        private bool _rDensity = false;
        private int _rMaxKeys = 5;
        private int _rMinKeys = 0;

        /// <summary>
        /// 是否修改单侧按键数量
        /// </summary>
        public bool ModifySingleSideKeyCount
        {
            get => _modifySingleSideKeyCount;
            set
            {
                _modifySingleSideKeyCount = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 单侧按键数量
        /// </summary>
        public int SingleSideKeyCount
        {
            get => _singleSideKeyCount;
            set
            {
                _singleSideKeyCount = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 左侧镜像
        /// </summary>
        public bool LMirror
        {
            get => _lMirror;
            set
            {
                _lMirror = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 左侧密度
        /// </summary>
        public bool LDensity
        {
            get => _lDensity;
            set
            {
                _lDensity = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 左侧最大键数
        /// </summary>
        public int LMaxKeys
        {
            get => _lMaxKeys;
            set
            {
                _lMaxKeys = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 左侧最小键数
        /// </summary>
        public int LMinKeys
        {
            get => _lMinKeys;
            set
            {
                _lMinKeys = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 右侧镜像
        /// </summary>
        public bool RMirror
        {
            get => _rMirror;
            set
            {
                _rMirror = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 右侧密度
        /// </summary>
        public bool RDensity
        {
            get => _rDensity;
            set
            {
                _rDensity = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 右侧最大键数
        /// </summary>
        public int RMaxKeys
        {
            get => _rMaxKeys;
            set
            {
                _rMaxKeys = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 右侧最小键数
        /// </summary>
        public int RMinKeys
        {
            get => _rMinKeys;
            set
            {
                _rMinKeys = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
