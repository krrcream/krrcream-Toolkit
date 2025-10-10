namespace krrTools.Tools.FilesManager
{
    public class FilesManagerInfo
    {
        // 可编辑属性
        public EditableProperty<string> Artist { get; } = new() { Value = string.Empty };
        public EditableProperty<string> Title { get; } = new() { Value = string.Empty };
        public EditableProperty<string> Creator { get; } = new() { Value = string.Empty };
        public EditableProperty<string> Diff { get; } = new() { Value = string.Empty };
        public EditableProperty<string> FilePath { get; } = new() { Value = string.Empty };
        public EditableProperty<double> OD { get; } = new();
        public EditableProperty<double> HP { get; } = new();
    
        // 只读属性
        public int Keys { get; init; }
        public int BeatmapID { get; init; }
        public int BeatmapSetID { get; init; } = -1;

        public void UndoAll()
        {
            Artist.Undo();
            Title.Undo();
            Creator.Undo();
            Diff.Undo();
            FilePath.Undo();
            OD.Undo();
            HP.Undo();
        }

        // protected virtual void OnPropertyChanged(string propertyName)
        // {
        //     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        // }
    }
}