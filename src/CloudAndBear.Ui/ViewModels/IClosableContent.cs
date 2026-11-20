namespace CloudAndBear.Ui.ViewModels;

/// <summary>Sadržaj prozora koji sam traži zatvaranje prozora (npr. terminal kad ljuska završi).</summary>
public interface IClosableContent
{
    event Action? CloseRequested;
}
