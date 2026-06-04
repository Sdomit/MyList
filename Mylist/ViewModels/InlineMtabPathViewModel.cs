namespace MyList.ViewModels;

public sealed class InlineMtabPathViewModel : ViewModelBase
{
    private string _pathText;

    public InlineMtabPathViewModel(string pathText)
    {
        _pathText = pathText;
    }

    public string PathText
    {
        get => _pathText;
        set => SetProperty(ref _pathText, value);
    }
}
