namespace ImmersiveDisplay.Services;

public interface IProtocolService
{
    void Register();
    void Unregister();
    void UpdateIfNecessary();
    bool IsRegistered();
    bool IsAssociationValid();
}
