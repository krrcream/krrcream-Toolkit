using System;
using krrTools.Configuration;

namespace krrTools.Tools.DPtool
{
    public class DPToolViewModel(DPToolOptions options) : ToolViewModelBase<DPToolOptions>(ConverterEnum.DP, false, options), IPreviewOptionsProvider
    {
        public bool ModifySingleSideKeyCount
        {
            get => Options.ModifySingleSideKeyCount;
            set
            {
                if (Options.ModifySingleSideKeyCount != value)
                {
                    Options.ModifySingleSideKeyCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public double SingleSideKeyCount
        {
            get => Options.SingleSideKeyCount;
            set
            {
                if (Math.Abs(Options.SingleSideKeyCount - value) > 1e-8)
                {
                    Options.SingleSideKeyCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool LMirror
        {
            get => Options.LMirror;
            set
            {
                if (Options.LMirror != value)
                {
                    Options.LMirror = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool LDensity
        {
            get => Options.LDensity;
            set
            {
                if (Options.LDensity != value)
                {
                    Options.LDensity = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool LRemove
        {
            get => Options.LRemove;
            set
            {
                if (Options.LRemove != value)
                {
                    Options.LRemove = value;
                    OnPropertyChanged();
                }
            }
        }

        public double LMaxKeys
        {
            get => Options.LMaxKeys;
            set
            {
                if (Math.Abs(Options.LMaxKeys - value) > 1e-8)
                {
                    Options.LMaxKeys = value;
                    OnPropertyChanged();
                    // 确保 LMinKeys 不超过 LMaxKeys
                    if (LMinKeys > LMaxKeys)
                        LMinKeys = LMaxKeys;
                }
            }
        }

        public double LMinKeys
        {
            get => Options.LMinKeys;
            set
            {
                if (Math.Abs(Options.LMinKeys - value) > 1e-8)
                {
                    Options.LMinKeys = value;
                    OnPropertyChanged();
                    // 确保 LMinKeys 不超过 LMaxKeys
                    if (LMinKeys > LMaxKeys)
                        LMinKeys = LMaxKeys;
                }
            }
        }

        public bool RMirror
        {
            get => Options.RMirror;
            set
            {
                if (Options.RMirror != value)
                {
                    Options.RMirror = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RDensity
        {
            get => Options.RDensity;
            set
            {
                if (Options.RDensity != value)
                {
                    Options.RDensity = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RRemove
        {
            get => Options.RRemove;
            set
            {
                if (Options.RRemove != value)
                {
                    Options.RRemove = value;
                    OnPropertyChanged();
                }
            }
        }

        public double RMaxKeys
        {
            get => Options.RMaxKeys;
            set
            {
                if (Math.Abs(Options.RMaxKeys - value) > 1e-8)
                {
                    Options.RMaxKeys = value;
                    OnPropertyChanged();
                    // 确保 RMinKeys 不超过 RMaxKeys
                    if (RMinKeys > RMaxKeys)
                        RMinKeys = RMaxKeys;
                }
            }
        }

        public double RMinKeys
        {
            get => Options.RMinKeys;
            set
            {
                if (Math.Abs(Options.RMinKeys - value) > 1e-8)
                {
                    Options.RMinKeys = value;
                    OnPropertyChanged();
                    // 确保 RMinKeys 不超过 RMaxKeys
                    if (RMinKeys > RMaxKeys)
                        RMinKeys = RMaxKeys;
                }
            }
        }

        public IToolOptions GetPreviewOptions() => new DPToolOptions
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
}