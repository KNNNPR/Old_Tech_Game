/// <summary>An optional left-click action alongside ordinary E interaction.</summary>
public interface IPrimaryInteractable
{
    bool CanPrimaryInteract(PlayerInteractor interactor);
    void PrimaryInteract(PlayerInteractor interactor);
}
