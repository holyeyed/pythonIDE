using System.Collections.ObjectModel;
namespace PythonEditor
{
    
    public class ProjectNodeModel
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; } // Phân biệt thư mục hay file
        public bool IsRoot { get; set; }
        // Trả về đường dẫn icon tương ứng
        public string IconPath => IsRoot? "pack://application:,,,/Images/folder.png" : IsDirectory
            ? "pack://application:,,,/Images/folder2.png"   
            : "pack://application:,,,/Images/file.png"; 

        public ObservableCollection<ProjectNodeModel> Children { get; set; } = new ObservableCollection<ProjectNodeModel>();
    }

    public class ProjectInfo
    {
        public string AppName { get; set; }
        public string DirectoryPath { get; set; }
    }

    public class ProjectConfig
    {
        public string AppName { get; set; } = "PythonApp";
        public string MainClass { get; set; } = "Main";
        public string Author { get; set; } = "Developer";
    }
}