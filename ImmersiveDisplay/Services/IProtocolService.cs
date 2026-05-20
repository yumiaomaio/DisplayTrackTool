namespace ImmersiveDisplay.Services;

public interface IProtocolService
{
    bool Register();
    bool Unregister();
    void UpdateIfNecessary();
    bool IsRegistered();
    bool IsAssociationValid();
}
