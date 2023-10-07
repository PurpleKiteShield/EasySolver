using ECommons.ExcelServices;

namespace EasySolver.UI.SearchableSettings;

internal interface ISearchable
{
    CheckBoxSearch Parent { get; set; }

    string SearchingKeys { get; }

    void Draw(Job job);
}
