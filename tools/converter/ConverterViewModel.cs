using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace krrTools.Tools.Converter
{
    public class ConverterViewModel : ObservableObject
    {
        private double _targetKeys = 10;
        private double _maxKeys = 10;
        private double _minKeys = 2;
        private double _transformSpeed = 4;
        private int? _seed = 114514;
        
        private bool _is4KSelected = false;
        private bool _is5KSelected = false;
        private bool _is6KSelected = false;
        private bool _is7KSelected = false;
        private bool _is8KSelected = false;
        private bool _is9KSelected = false;
        private bool _is10KSelected = false;
        private bool _is10KPlusSelected = false;
        
        public bool Is4KSelected
        {
            get => _is4KSelected;
            set => SetProperty(ref _is4KSelected, value);
        }

        public bool Is5KSelected
        {
            get => _is5KSelected;
            set => SetProperty(ref _is5KSelected, value);
        }

        public bool Is6KSelected
        {
            get => _is6KSelected;
            set => SetProperty(ref _is6KSelected, value);
        }

        public bool Is7KSelected
        {
            get => _is7KSelected;
            set => SetProperty(ref _is7KSelected, value);
        }

        public bool Is8KSelected
        {
            get => _is8KSelected;
            set => SetProperty(ref _is8KSelected, value);
        }

        public bool Is9KSelected
        {
            get => _is9KSelected;
            set => SetProperty(ref _is9KSelected, value);
        }

        public bool Is10KSelected
        {
            get => _is10KSelected;
            set => SetProperty(ref _is10KSelected, value);
        }

        public bool Is10KPlusSelected
        {
            get => _is10KPlusSelected;
            set => SetProperty(ref _is10KPlusSelected, value);
        }
        
        public int? Seed
        {
            get => _seed;
            set => SetProperty(ref _seed, value);
        }

        // 获取选中的键数列表
        public List<int> GetSelectedKeyTypes()
        {
            var selectedKeys = new List<int>();
    
            if (Is4KSelected) selectedKeys.Add(4);
            if (Is5KSelected) selectedKeys.Add(5);
            if (Is6KSelected) selectedKeys.Add(6);
            if (Is7KSelected) selectedKeys.Add(7);
            if (Is8KSelected) selectedKeys.Add(8);
            if (Is9KSelected) selectedKeys.Add(9);
            if (Is10KSelected) selectedKeys.Add(10);
            if (Is10KPlusSelected) selectedKeys.Add(11); // 10K+用11表示
    
            return selectedKeys;
        }
        
        public double TargetKeys
        {
            get => _targetKeys;
            set
            {
                SetProperty(ref _targetKeys, value);
                // 更新MaxKeys的最大值，并确保MaxKeys不超过TargetKeys
                if (MaxKeys > TargetKeys)
                    MaxKeys = TargetKeys;
                else
                    MaxKeys = TargetKeys; // 保持最大键数滑块在最右边
                
                // 更新MinKeys的最大值
                if (MinKeys > MaxKeys)
                    MinKeys = MaxKeys;
            }
        }

        public double MaxKeys
        {
            get => _maxKeys;
            set 
            {
                if (SetProperty(ref _maxKeys, value))
                {
                    // 当最大键数改变时，更新最小键数
                    // 如果最大键数等于1，最小键数等于1；否则最小键数等于2
                    if (_maxKeys == 1)
                        MinKeys = 1;
                    else
                        MinKeys = 2;
            
                    // 确保MinKeys不超过MaxKeys
                    if (MinKeys > MaxKeys)
                        MinKeys = MaxKeys;
                    OnPropertyChanged(nameof(MinKeysMaximum));
                }
            }
        }
        
        public double MinKeys
        {
            get => _minKeys;
            set => SetProperty(ref _minKeys, value);
        }
        
       public double MinKeysMaximum => MaxKeys;
        
        public double TransformSpeed
        {
            get => _transformSpeed;
            set
            {
                if (SetProperty(ref _transformSpeed, value))
                {
                    // 当 TransformSpeed 更新时，通知 TransformSpeedDisplay 也已更新
                    OnPropertyChanged(nameof(TransformSpeedDisplay));
                }
            }
        }

        public string TransformSpeedDisplay
        {
            get
            {
                switch ((int)_transformSpeed)
                {
                    case 0: return "1/16";
                    case 1: return "1/8";
                    case 2: return "1/4";
                    case 3: return "1/2";
                    case 4: return "1";
                    case 5: return "2";
                    case 6: return "4";
                    case 7: return "8";
                    case 8: return "∞";
                    default: return "1";
                }
            }
        }
        

        public ConversionOptions? GetConversionOptions()
        {
            // 定义滑块值到实际速度值的映射
            double[] speedMap = { 0.0625, 0.125, 0.25, 0.5, 1, 2, 4, 8, 9999 };
            int sliderValue = (int)this.TransformSpeed;
            double actualSpeed = (sliderValue >= 0 && sliderValue < speedMap.Length) 
                ? speedMap[sliderValue] 
                : 1; // 默认值
            
            var selectedKeys = GetSelectedKeyTypes();
            
            return new ConversionOptions
            {
                TargetKeys = this.TargetKeys,
                MaxKeys = this.MaxKeys,
                MinKeys = this.MinKeys,
                TransformSpeed = actualSpeed,
                SelectedKeyTypes = selectedKeys,
                Seed = this.Seed
            };
        }
        
    }
}